using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Sync
{
    /// <summary>
    /// The pure, additive reconciliation between a Jellyfin playlist's current contents and the
    /// tracks a Deezer import matched in the library. It answers one question — "which matched item
    /// ids are not already in the playlist?" — preserving Deezer order and de-duplicating. Additive
    /// only: it never returns removals, so tracks a user added by hand (and tracks dropped from the
    /// Deezer playlist) are left untouched. Pure so the sync policy is fully unit-testable.
    /// </summary>
    public static class PlaylistSync
    {
        /// <summary>
        /// Computes the item ids to append to a playlist: every candidate not already present, in
        /// candidate order, with duplicates collapsed.
        /// </summary>
        /// <param name="existingItemIds">The ids already in the Jellyfin playlist.</param>
        /// <param name="candidateItemIds">The matched library ids from the Deezer import, in order.</param>
        /// <returns>The ids to add (possibly empty), in order, de-duplicated.</returns>
        public static IReadOnlyList<string> ComputeAdditions(
            IEnumerable<string> existingItemIds,
            IEnumerable<string> candidateItemIds)
        {
            var present = new HashSet<string>(existingItemIds);
            var additions = new List<string>();

            foreach (var id in candidateItemIds)
            {
                if (!string.IsNullOrEmpty(id) && present.Add(id))
                {
                    additions.Add(id);
                }
            }

            return additions;
        }
    }
}
