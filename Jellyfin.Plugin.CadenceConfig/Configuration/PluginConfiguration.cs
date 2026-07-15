using System;
using Jellyfin.Plugin.CadenceConfig.Sync;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CadenceConfig.Configuration
{
    /// <summary>
    /// Plugin configuration for CadenceConfig — the server-operator-set values every Cadence
    /// client (web + native iOS) fetches at sign-in, so no user has to configure URLs by hand.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration()
        {
            MarlinUrl = string.Empty;
            SignupUrl = string.Empty;
            CastReceiverAppId = string.Empty;
            LidarrUrl = string.Empty;
            LidarrApiKey = string.Empty;
            SyncIntervalHours = 12;
            DeezerSubscriptions = Array.Empty<DeezerSubscription>();
        }

        /// <summary>
        /// Gets or sets the marlin-search (Meilisearch) base URL for faster search. Handed to
        /// clients as a non-secret — marlin's /search is unauthenticated, so no token is needed.
        /// </summary>
        public string MarlinUrl { get; set; }

        /// <summary>
        /// Gets or sets an optional sign-up URL shown on the client's sign-in screen (e.g. an
        /// invite/registration page for this server's Jellyfin).
        /// </summary>
        public string SignupUrl { get; set; }

        /// <summary>
        /// Gets or sets an optional Google Cast receiver application id for the client's casting.
        /// </summary>
        public string CastReceiverAppId { get; set; }

        /// <summary>
        /// Gets or sets the Lidarr base URL (e.g. http://localhost:8686). When set with an API key,
        /// the plugin proxies the client's "request missing music" calls to Lidarr, injecting the
        /// key SERVER-SIDE so it never reaches the browser or the app.
        /// </summary>
        public string LidarrUrl { get; set; }

        /// <summary>
        /// Gets or sets the Lidarr API key. A WRITE credential — it is NEVER sent to clients; the
        /// plugin's Lidarr proxy attaches it to outbound requests inside the Jellyfin server only.
        /// </summary>
        public string LidarrApiKey { get; set; }

        /// <summary>
        /// Gets or sets how often (in hours) the scheduled task re-syncs every Deezer subscription,
        /// adding newly-available library tracks to each mirrored Jellyfin playlist.
        /// </summary>
        public int SyncIntervalHours { get; set; }

        /// <summary>
        /// Gets or sets the persisted Deezer→Jellyfin playlist subscriptions the scheduled task keeps
        /// in sync. An array because Jellyfin serializes plugin configuration as XML, which round-trips
        /// arrays cleanly (CA1819 suppressed in .editorconfig with that justification).
        /// </summary>
        public DeezerSubscription[] DeezerSubscriptions { get; set; }
    }
}
