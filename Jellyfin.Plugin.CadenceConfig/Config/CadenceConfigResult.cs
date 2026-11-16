using Jellyfin.Plugin.CadenceConfig.Configuration;

namespace Jellyfin.Plugin.CadenceConfig.Config
{
    /// <summary>
    /// The runtime config the Cadence client fetches at sign-in and merges into its
    /// <c>window.__CADENCE_CONFIG__</c>. Non-secret values only: no API key is EVER included (not
    /// Lidarr's, not Music Grabber's) — every signed-in Jellyfin user can read this response.
    /// <see cref="LidarrProxy"/> tells the client the proxy endpoint is available instead.
    /// </summary>
    public class CadenceConfigResult
    {
        /// <summary>
        /// Gets or sets the marlin-search base URL (empty when unset). marlin's /search is
        /// unauthenticated, so this is safe to hand to any client.
        /// </summary>
        public string MarlinUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional sign-up URL for the client's sign-in screen (empty when unset).
        /// </summary>
        public string SignupUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional Google Cast receiver app id (empty when unset).
        /// </summary>
        public string CastReceiverAppId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the server's Lidarr "request music" proxy is
        /// available (Lidarr URL + key are both configured). The client shows the Requests feature
        /// and calls the plugin's proxy endpoint; the key itself is never sent.
        /// </summary>
        public bool LidarrProxy { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Deezer playlist-import endpoint is available.
        /// Always true when this plugin is installed — Deezer's public playlist API needs no config
        /// or key — so the client can surface the "import a Deezer playlist" flow.
        /// </summary>
        public bool DeezerImport { get; set; }

        /// <summary>
        /// Gets a value indicating whether the retired precomputed Home-shelves endpoint
        /// (GET /Cadence/Home) is available. ALWAYS false: the endpoint was removed because native
        /// Jellyfin 12 queries are fast enough. Kept on the wire as an explicit <c>false</c> so
        /// Cadence clients already in the wild (which only take the plugin path on an explicit
        /// <c>true</c>) stay on their native Home path and never call the missing endpoint.
        /// </summary>
        public bool HomeShelves => false;

        /// <summary>
        /// Gets a value indicating whether the retired precomputed audiobook-library endpoint
        /// (GET /Cadence/Audiobooks) is available. ALWAYS false for the same reason as
        /// <see cref="HomeShelves"/>: old clients keep using the native AudioBook query.
        /// </summary>
        public bool Audiobooks => false;

        /// <summary>
        /// Gets or sets the Music Grabber base URL (empty when unset). The URL only — its API key
        /// stays server-side (it is used by the Deezer import's grab fulfillment), because handing it
        /// to every signed-in user let any of them drive the grabber directly.
        /// </summary>
        public string MusicGrabberUrl { get; set; } = string.Empty;

        /// <summary>
        /// Builds the client-facing config from the plugin configuration, deliberately omitting every
        /// API key and only surfacing a boolean for whether the Lidarr proxy is usable.
        /// </summary>
        /// <param name="config">The plugin configuration.</param>
        /// <returns>The non-secret config for the client.</returns>
        public static CadenceConfigResult FromConfiguration(PluginConfiguration config)
        {
            return new CadenceConfigResult
            {
                MarlinUrl = config.MarlinUrl ?? string.Empty,
                SignupUrl = config.SignupUrl ?? string.Empty,
                CastReceiverAppId = config.CastReceiverAppId ?? string.Empty,
                LidarrProxy = !string.IsNullOrWhiteSpace(config.LidarrUrl)
                    && !string.IsNullOrWhiteSpace(config.LidarrApiKey),
                DeezerImport = true, // plugin serving this response IS the import endpoint's host
                MusicGrabberUrl = config.MusicGrabberUrl ?? string.Empty,
            };
        }
    }
}
