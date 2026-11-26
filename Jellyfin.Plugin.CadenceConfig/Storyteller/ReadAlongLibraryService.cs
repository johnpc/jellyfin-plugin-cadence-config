using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Builds <c>GET /Cadence/Storyteller/Books</c>: one Storyteller book-list read (cached by
    /// <see cref="BookCatalog"/>), one Jellyfin AudioBook enumeration, a title/author match per book and the
    /// cached timeline when there is one. Decisions live in <see cref="LibraryStatus"/>; this is plumbing.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class ReadAlongLibraryService
    {
        private readonly BookCatalog _catalog;
        private readonly TimelineCache _timelines;
        private readonly JellyfinBooks _jellyfin;
        private readonly UserProvisioner _provisioner;
        private readonly LibraryResponseCache _cache;

        /// <summary>Initializes a new instance of the <see cref="ReadAlongLibraryService"/> class.</summary>
        /// <param name="catalog">The Storyteller book list per token.</param>
        /// <param name="timelines">Cached timelines.</param>
        /// <param name="jellyfin">Jellyfin library plumbing.</param>
        /// <param name="provisioner">Creates the caller's Storyteller account when an admin login exists.</param>
        /// <param name="cache">Per-caller response cache.</param>
        public ReadAlongLibraryService(BookCatalog catalog, TimelineCache timelines, JellyfinBooks jellyfin, UserProvisioner provisioner, LibraryResponseCache cache)
        {
            _catalog = catalog;
            _timelines = timelines;
            _jellyfin = jellyfin;
            _provisioner = provisioner;
            _cache = cache;
        }

        /// <summary>The caller's read-along library listing.</summary>
        /// <param name="callerId">The verified caller.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The listing.</returns>
        public async Task<ReadAlongLibrary> ForAsync(Guid callerId, CancellationToken cancellationToken)
        {
            var fresh = _cache.Fresh(callerId);
            if (fresh != null)
            {
                return fresh;
            }

            var result = await BuildAsync(callerId, cancellationToken).ConfigureAwait(false);
            _cache.Put(callerId, result);
            return result;
        }

        private async Task<ReadAlongLibrary> BuildAsync(Guid callerId, CancellationToken cancellationToken)
        {
            var config = Plugin.GetConfiguration();
            var baseUrl = StorytellerUrls.Base(config.StorytellerUrl);
            if (baseUrl == null)
            {
                return LibraryStatus.Unavailable(LibraryState.Unconfigured);
            }

            var link = LinkStore.Find(config, callerId);
            if (link == null)
            {
                var username = _jellyfin.UserOf(callerId)?.Username;
                link = username == null ? null : await _provisioner.EnsureLinkAsync(callerId, username, cancellationToken).ConfigureAwait(false);
            }

            if (link == null)
            {
                return LibraryStatus.Unavailable(LibraryState.Unlinked);
            }

            var maxAge = _cache.WasAligning(callerId) ? BookCatalog.UnalignedTtl : BookCatalog.Ttl;
            var library = await _catalog.BooksAsync(baseUrl, link.Token, maxAge, cancellationToken).ConfigureAwait(false);
            if (library == null)
            {
                // Storyteller unreachable: no verdicts rather than a wall of "unmatched".
                return LibraryStatus.Checked(Array.Empty<(string, ReadAlongStatus)>());
            }

            var rows = new List<(string, ReadAlongStatus)>();
            foreach (var (anchor, parts) in PartOrder.Books(_jellyfin.AllParts()))
            {
                var book = BookMatcher.Match(new[] { anchor.Album, anchor.Name }, _jellyfin.AuthorOf(anchor.ItemId), library);
                Timeline? reconciled = null;
                if (book != null && !string.IsNullOrEmpty(book.AlignedAt))
                {
                    var cached = await _timelines.GetAsync(book.Uuid, book.Fingerprint, cancellationToken).ConfigureAwait(false);
                    var total = PartsTimeline.Total(parts);
                    reconciled = cached == null ? null : ReadAlongService.Reconcile(cached, total, _jellyfin.ChapterStarts(parts));
                }

                rows.Add((anchor.ItemId, LibraryStatus.For(book, reconciled, PartsTimeline.Total(parts))));
            }

            return LibraryStatus.Checked(rows);
        }
    }
}
