using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>
    /// The outcome of matching a Deezer playlist to a library: owned item ids to add to a Jellyfin
    /// playlist, and distinct artist names whose tracks are missing (candidates for a Lidarr request).
    /// </summary>
    /// <param name="FoundItemIds">Jellyfin item ids present in the library, in playlist order.</param>
    /// <param name="MissingArtists">Distinct artist names not found (for Lidarr requests).</param>
    public sealed record DeezerMatchResult(
        IReadOnlyList<string> FoundItemIds,
        IReadOnlyList<string> MissingArtists)
    {
        /// <summary>Gets the count of found tracks.</summary>
        public int FoundCount => FoundItemIds.Count;

        /// <summary>Gets the count of distinct missing artists.</summary>
        public int MissingArtistCount => MissingArtists.Count;
    }
}
