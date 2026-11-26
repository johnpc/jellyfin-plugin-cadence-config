using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// The Storyteller library as seen by one token, cached briefly (a book list is fetched once per
    /// user per few minutes, not once per request), plus the title/author match on top.
    /// </summary>
    public sealed class BookCatalog
    {
        /// <summary>How long a fetched book list is reused.</summary>
        public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

        /// <summary>
        /// How long a list is trusted when the book asked about is still unaligned. Alignment finishing
        /// is the one change a listener is actively waiting on ("Still aligning… check back soon"), so
        /// a stale list must not make them wait a further five minutes. Verified live on 2026-11-21.
        /// </summary>
        public static readonly TimeSpan UnalignedTtl = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How long to stop asking after Storyteller refused or timed out. Without it every audiobook
        /// position save (one per ~10 s per listener) retried and logged a warning while Storyteller
        /// was down for maintenance — ~400 warnings in an hour on 2026-11-22. One attempt a minute is plenty.
        /// </summary>
        public static readonly TimeSpan OutageBackoff = TimeSpan.FromSeconds(60);

        private readonly StorytellerClient _client;
        private readonly Func<DateTimeOffset> _now;
        private readonly ConcurrentDictionary<string, (DateTimeOffset At, IReadOnlyList<StorytellerBook> Books)> _byToken = new();
        private readonly ConcurrentDictionary<string, DateTimeOffset> _failedAt = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="BookCatalog"/> class.
        /// </summary>
        /// <param name="client">The Storyteller client.</param>
        public BookCatalog(StorytellerClient client)
            : this(client, () => DateTimeOffset.UtcNow)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BookCatalog"/> class with an injectable clock.
        /// </summary>
        /// <param name="client">The Storyteller client.</param>
        /// <param name="now">The clock.</param>
        public BookCatalog(StorytellerClient client, Func<DateTimeOffset> now)
        {
            _client = client;
            _now = now;
        }

        /// <summary>The user's Storyteller books (cached), or null when Storyteller can't be read.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's token.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The books, or null.</returns>
        public Task<IReadOnlyList<StorytellerBook>?> BooksAsync(string baseUrl, string token, CancellationToken cancellationToken)
        {
            return BooksAsync(baseUrl, token, Ttl, cancellationToken);
        }

        /// <summary>Finds the Storyteller book for a Jellyfin title + author, or null.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's token.</param>
        /// <param name="title">The Jellyfin book title.</param>
        /// <param name="author">The Jellyfin author.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The match, or null.</returns>
        public Task<StorytellerBook?> MatchAsync(string baseUrl, string token, string? title, string? author, CancellationToken cancellationToken)
        {
            return MatchAsync(baseUrl, token, new[] { title }, author, cancellationToken);
        }

        /// <summary>Finds the Storyteller book for the first matching Jellyfin title (album tag, then name) + author.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's token.</param>
        /// <param name="titles">Candidate Jellyfin titles, most specific first.</param>
        /// <param name="author">The Jellyfin author.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The match, or null.</returns>
        public async Task<StorytellerBook?> MatchAsync(string baseUrl, string token, IReadOnlyList<string?> titles, string? author, CancellationToken cancellationToken)
        {
            var books = await BooksAsync(baseUrl, token, cancellationToken).ConfigureAwait(false);
            var match = books == null ? null : BookMatcher.Match(titles, author, books);
            if (match == null || !string.IsNullOrEmpty(match.AlignedAt))
            {
                return match;
            }

            // The matched book is still aligning: re-read the list on the short TTL so the moment
            // Storyteller finishes, the next request sees it.
            var fresh = await BooksAsync(baseUrl, token, UnalignedTtl, cancellationToken).ConfigureAwait(false);
            return fresh == null ? match : BookMatcher.Match(titles, author, fresh) ?? match;
        }

        /// <summary>Drops the cached list for a token (after a link is replaced).</summary>
        /// <param name="token">The token.</param>
        public void Forget(string token)
        {
            _byToken.TryRemove(token, out _);
            _failedAt.TryRemove(token, out _);
        }

        /// <summary>The user's Storyteller books, reusing a cached list no older than <paramref name="maxAge"/>.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's token.</param>
        /// <param name="maxAge">How old a cached list may be (<see cref="Ttl"/> or <see cref="UnalignedTtl"/>).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The books, or null.</returns>
        public async Task<IReadOnlyList<StorytellerBook>?> BooksAsync(string baseUrl, string token, TimeSpan maxAge, CancellationToken cancellationToken)
        {
            var now = _now();
            if (_byToken.TryGetValue(token, out var cached) && now - cached.At < maxAge)
            {
                return cached.Books;
            }

            if (_failedAt.TryGetValue(token, out var failed) && now - failed < OutageBackoff)
            {
                // Storyteller was unreachable a moment ago: serve the stale list if there is one, else nothing.
                return cached.Books;
            }

            var books = await _client.ListBooksAsync(baseUrl, token, cancellationToken).ConfigureAwait(false);
            if (books != null)
            {
                _byToken[token] = (_now(), books);
                _failedAt.TryRemove(token, out _);
            }
            else
            {
                _failedAt[token] = _now();
            }

            return books;
        }
    }
}
