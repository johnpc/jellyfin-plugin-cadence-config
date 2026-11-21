using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Configuration;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Resolves a Jellyfin audiobook for a caller into its Storyteller counterpart: link → parts →
    /// matched book → (cached) timeline → explicit <see cref="ReadAlongStatus"/>. The status decision
    /// is in the pure <see cref="StatusFor"/>; the rest is orchestration over injected pieces.
    /// </summary>
    public sealed class ReadAlongService
    {
        private readonly BookCatalog _catalog;
        private readonly TimelineBuilder _timelines;
        private readonly JellyfinBooks _jellyfin;
        private readonly Func<PluginConfiguration> _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadAlongService"/> class.
        /// </summary>
        /// <param name="catalog">The Storyteller catalog.</param>
        /// <param name="timelines">The timeline builder.</param>
        /// <param name="jellyfin">Jellyfin library plumbing.</param>
        public ReadAlongService(BookCatalog catalog, TimelineBuilder timelines, JellyfinBooks jellyfin)
            : this(catalog, timelines, jellyfin, Plugin.GetConfiguration)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadAlongService"/> class with an explicit config source.
        /// </summary>
        /// <param name="catalog">The Storyteller catalog.</param>
        /// <param name="timelines">The timeline builder.</param>
        /// <param name="jellyfin">Jellyfin library plumbing.</param>
        /// <param name="config">The configuration source.</param>
        public ReadAlongService(BookCatalog catalog, TimelineBuilder timelines, JellyfinBooks jellyfin, Func<PluginConfiguration> config)
        {
            _catalog = catalog;
            _timelines = timelines;
            _jellyfin = jellyfin;
            _config = config;
        }

        /// <summary>The explicit status for what was (and wasn't) resolved.</summary>
        /// <param name="linked">Whether the caller is linked.</param>
        /// <param name="book">The matched Storyteller book, if any.</param>
        /// <param name="timeline">The timeline, if built.</param>
        /// <param name="partsTotal">The Jellyfin book length in seconds.</param>
        /// <returns>The status.</returns>
        public static ReadAlongStatus StatusFor(bool linked, StorytellerBook? book, Timeline? timeline, double partsTotal)
        {
            if (!linked)
            {
                return ReadAlongStatus.Unlinked;
            }

            if (book == null)
            {
                return ReadAlongStatus.Unmatched;
            }

            if (string.IsNullOrEmpty(book.AlignedAt) || timeline == null)
            {
                return ReadAlongStatus.Aligning;
            }

            return OverlayTimeline.Check(timeline, partsTotal);
        }

        /// <summary>Shapes a resolution into the wire response.</summary>
        /// <param name="resolved">The resolution.</param>
        /// <returns>The response.</returns>
        public static ReadAlongBook ToResponse(ResolvedBook resolved)
        {
            var ready = resolved.Status == ReadAlongStatus.Ready && resolved.Timeline != null;
            return new ReadAlongBook
            {
                Kind = resolved.Status,
                BookUuid = resolved.Book?.Uuid,
                Title = resolved.Book?.Title,
                Duration = PartsTimeline.Total(resolved.Parts),
                Parts = resolved.Parts.Select(p => new ReadAlongPart { ItemId = p.ItemId, Duration = p.Duration }).ToList(),
                Chapters = ready ? resolved.Timeline!.Chapters : Array.Empty<ReadAlongChapter>(),
                Clips = ready ? resolved.Timeline!.Clips : Array.Empty<ReadAlongClip>(),
            };
        }

        /// <summary>Resolves a caller's audiobook. Null when the item isn't an audiobook or Storyteller isn't configured.</summary>
        /// <param name="callerId">The Jellyfin user.</param>
        /// <param name="itemId">Any part of the book.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The resolution, or null.</returns>
        [ExcludeFromCodeCoverage]
        public async Task<ResolvedBook?> ResolveAsync(Guid callerId, Guid itemId, CancellationToken cancellationToken)
        {
            var config = _config();
            var baseUrl = StorytellerUrls.Base(config.StorytellerUrl);
            var found = _jellyfin.BookOf(itemId);
            if (baseUrl == null || found == null)
            {
                return null;
            }

            var (anchor, parts) = found.Value;
            var link = LinkStore.Find(config, callerId);
            if (link == null)
            {
                return new ResolvedBook(new StorytellerLink(), baseUrl, anchor, parts, null, null, ReadAlongStatus.Unlinked);
            }

            return await ResolveLinkedAsync(link, baseUrl, anchor, parts, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Resolves a book for a user already known to be linked.</summary>
        /// <param name="link">The link.</param>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="anchor">The book's anchor part.</param>
        /// <param name="parts">The book's parts.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The resolution.</returns>
        [ExcludeFromCodeCoverage]
        public async Task<ResolvedBook> ResolveLinkedAsync(StorytellerLink link, string baseUrl, PartInfo anchor, IReadOnlyList<BookPart> parts, CancellationToken cancellationToken)
        {
            var title = string.IsNullOrWhiteSpace(anchor.Album) ? anchor.Name : anchor.Album;
            var author = _jellyfin.AuthorOf(anchor.ItemId);
            var book = await _catalog.MatchAsync(baseUrl, link.Token, title, author, cancellationToken).ConfigureAwait(false);
            Timeline? timeline = null;
            if (book != null && !string.IsNullOrEmpty(book.AlignedAt))
            {
                timeline = await _timelines.GetAsync(baseUrl, link.Token, book, cancellationToken).ConfigureAwait(false);
            }

            var status = StatusFor(true, book, timeline, PartsTimeline.Total(parts));
            return new ResolvedBook(link, baseUrl, anchor, parts, book, timeline, status);
        }
    }
}
