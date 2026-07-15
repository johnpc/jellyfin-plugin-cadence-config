using System;

namespace Jellyfin.Plugin.CadenceConfig.Sync
{
    /// <summary>
    /// A persisted link between a public Deezer playlist and the Jellyfin playlist that mirrors it
    /// for one user. The scheduled sync task walks these to keep each Jellyfin playlist up to date
    /// as the user's library grows (e.g. after Lidarr fills in a previously-missing artist). Stored
    /// in plugin configuration, so it round-trips as plain serializable properties.
    /// </summary>
    public class DeezerSubscription
    {
        /// <summary>Gets or sets the numeric Deezer playlist id (the sync source).</summary>
        public string DeezerPlaylistId { get; set; } = string.Empty;

        /// <summary>Gets or sets the Jellyfin playlist id kept in sync (the mirror target).</summary>
        public string JellyfinPlaylistId { get; set; } = string.Empty;

        /// <summary>Gets or sets the owning Jellyfin user id (playlist owner + library scope).</summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the artists whose tracks weren't in the library as of the last import/sync —
        /// the Lidarr-request candidates the client shows on the playlist page. Persisted so the list
        /// survives across sessions; the read endpoint recomputes it against the current library so a
        /// now-owned artist drops off immediately (this is the last-known fallback). An array so it
        /// round-trips through Jellyfin's XML config serialization cleanly (CA1819 suppressed).
        /// </summary>
        public string[] MissingArtists { get; set; } = Array.Empty<string>();
    }
}
