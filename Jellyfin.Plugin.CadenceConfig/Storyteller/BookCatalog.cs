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

        private readonly StorytellerClient _client;
        private readonly ConcurrentDictionary<string, (DateTimeOffset At, IReadOnlyList<StorytellerBook> Books)> _byToken = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="BookCatalog"/> class.
        /// </summary>
        /// <param name="client">The Storyteller client.</param>
        public BookCatalog(StorytellerClient client)
        {
            _client = client;
        }

        /// <summary>The user's Storyteller books (cached), or null when Storyteller can't be read.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's token.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The books, or null.</returns>
        public async Task<IReadOnlyList<StorytellerBook>?> BooksAsync(string baseUrl, string token, CancellationToken cancellationToken)
        {
            if (_byToken.TryGetValue(token, out var cached) && DateTimeOffset.UtcNow - cached.At < Ttl)
            {
                return cached.Books;
            }

            var books = await _client.ListBooksAsync(baseUrl, token, cancellationToken).ConfigureAwait(false);
            if (books != null)
            {
                _byToken[token] = (DateTimeOffset.UtcNow, books);
            }

            return books;
        }

        /// <summary>Finds the Storyteller book for a Jellyfin title + author, or null.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's token.</param>
        /// <param name="title">The Jellyfin book title.</param>
        /// <param name="author">The Jellyfin author.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The match, or null.</returns>
        public async Task<StorytellerBook?> MatchAsync(string baseUrl, string token, string? title, string? author, CancellationToken cancellationToken)
        {
            var books = await BooksAsync(baseUrl, token, cancellationToken).ConfigureAwait(false);
            return books == null ? null : BookMatcher.Match(title, author, books);
        }

        /// <summary>Drops the cached list for a token (after a link is replaced).</summary>
        /// <param name="token">The token.</param>
        public void Forget(string token) => _byToken.TryRemove(token, out _);
    }
}
