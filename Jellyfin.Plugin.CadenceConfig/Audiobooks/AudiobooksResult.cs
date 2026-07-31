using System.Collections.Generic;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.CadenceConfig.Audiobooks
{
    /// <summary>
    /// The precomputed audiobook library for one user, returned by GET /Cadence/Audiobooks in a
    /// SINGLE response so the Cadence client skips the slow recursive AudioBook scan it otherwise
    /// runs on every first load (up to 5000 files across all books — 7–19s on a large library).
    /// <see cref="Books"/> holds the same <see cref="BaseItemDto"/> shape the client already consumes
    /// from Jellyfin's /Items endpoint, so the client groups it into logical books with no
    /// special-casing (see the client's audiobookSource adapter + groupBooks). The "Continue
    /// listening" / favorites highlights are NOT served here — they're small, bounded, and volatile
    /// (resume position changes constantly), so the client keeps fetching them natively for live data.
    /// </summary>
    public class AudiobooksResult
    {
        /// <summary>Gets every audiobook file in the library, sorted by SortName ascending — the flat
        /// list the client groups into books. The same order/shape as
        /// /Items?IncludeItemTypes=AudioBook&amp;Recursive=true&amp;SortBy=SortName.</summary>
        public IReadOnlyList<BaseItemDto> Books { get; init; } = new List<BaseItemDto>();
    }
}
