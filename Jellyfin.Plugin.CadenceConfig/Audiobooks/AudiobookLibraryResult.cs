using System.Collections.Generic;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.CadenceConfig.Audiobooks
{
    /// <summary>
    /// The precomputed audiobook library the Cadence client fetches in ONE call
    /// (GET /Cadence/Audiobooks), replacing the client's slow recursive AudioBook scan
    /// (up to 5000 files, 4–19s on a large library). The plugin builds and caches this
    /// list, so opening the Audiobooks tab is a fast cache hit. Only the STATIC catalog
    /// is served (titles/art/grouping/runtimes); the client overlays live per-book
    /// reading progress from its own bounded query, so a cached DTO never shows a stale
    /// progress bar. <c>Books</c> matches the shape of Jellyfin's own /Items response so
    /// the client's grouping works unchanged.
    /// </summary>
    public class AudiobookLibraryResult
    {
        /// <summary>
        /// Gets the audiobook files (Type = AudioBook), SortName ascending — the same order
        /// the native scan returns, so the client's first-appearance grouping stays stable.
        /// </summary>
        public IReadOnlyList<BaseItemDto> Books { get; init; } = new List<BaseItemDto>();
    }
}
