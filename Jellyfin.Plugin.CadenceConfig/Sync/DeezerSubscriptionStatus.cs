using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Sync
{
    /// <summary>
    /// The client-facing status of a mirrored Deezer playlist: which Deezer playlist it came from and
    /// the artists whose tracks aren't in the library yet (recomputed against the current library, so
    /// a now-owned artist is already gone). Drives the persistent "request these" list on the playlist
    /// page — see the Cadence client's playlist detail.
    /// </summary>
    /// <param name="DeezerPlaylistId">The numeric Deezer playlist id the Jellyfin playlist mirrors.</param>
    /// <param name="MissingArtists">Distinct artist names still missing (Lidarr-request candidates).</param>
    public sealed record DeezerSubscriptionStatus(
        string DeezerPlaylistId,
        IReadOnlyList<string> MissingArtists);
}
