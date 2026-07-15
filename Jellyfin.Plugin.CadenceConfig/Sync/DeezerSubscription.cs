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
    }
}
