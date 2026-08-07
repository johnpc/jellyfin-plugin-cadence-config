using System.Collections.Generic;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.CadenceConfig.Audiobooks
{
    /// <summary>
    /// The <see cref="ItemFields"/> the audiobook-library DTOs must carry — the exact set the Cadence
    /// client consumes. Kept as a pure, unit-testable constant (no Jellyfin services) so the contract
    /// with the client is asserted in tests: grouping needs Album/ParentId/IndexNumber, and the detail
    /// page needs Overview/Genres/ProductionYear/DateCreated (which Jellyfin omits from a default DTO).
    /// </summary>
    public static class AudiobookLibraryFields
    {
        /// <summary>
        /// Gets the fields requested for every audiobook DTO. Grouping fields
        /// (Album/ParentId are implicit on the DTO; IndexNumber too) plus the display fields the
        /// client's list and detail page read.
        /// </summary>
        public static IReadOnlyList<ItemFields> Fields { get; } = new[]
        {
            ItemFields.Overview,
            ItemFields.Genres,
            ItemFields.ParentId,
            ItemFields.DateCreated,
            ItemFields.SortName,
        };
    }
}
