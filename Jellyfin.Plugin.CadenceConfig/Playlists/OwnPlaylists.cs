using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Playlists
{
    /// <summary>
    /// Picks the playlists a user OWNS. Jellyfin's item list has no owner field, so the Cadence
    /// client used to prove ownership one playlist at a time (the owner-only share endpoint); the
    /// server knows the owner of every playlist, so it can answer in one call. Pure, so it is tested
    /// without a library.
    /// </summary>
    public static class OwnPlaylists
    {
        /// <summary>
        /// Gets the ids (Jellyfin's dash-less "N" form, as the item API returns them) of the
        /// playlists in <paramref name="playlists"/> owned by <paramref name="userId"/> — never a
        /// playlist merely shared with or visible to them.
        /// </summary>
        /// <param name="userId">The calling user.</param>
        /// <param name="playlists">Each candidate playlist's id and owner.</param>
        /// <returns>The owned playlist ids, de-duplicated, in input order.</returns>
        public static IReadOnlyList<string> For(Guid userId, IEnumerable<(Guid Id, Guid OwnerUserId)> playlists)
        {
            if (userId == Guid.Empty)
            {
                return Array.Empty<string>();
            }

            return playlists
                .Where(p => p.OwnerUserId == userId && p.Id != Guid.Empty)
                .Select(p => p.Id.ToString("N"))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }
}
