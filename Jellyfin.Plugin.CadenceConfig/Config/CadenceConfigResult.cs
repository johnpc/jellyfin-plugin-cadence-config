using Jellyfin.Plugin.CadenceConfig.Configuration;

namespace Jellyfin.Plugin.CadenceConfig.Config
{
    /// <summary>
    /// The runtime config the Cadence client fetches at sign-in and merges into its
    /// <c>window.__CADENCE_CONFIG__</c>. Non-secret values only — the Lidarr API key is NEVER
    /// included; instead <see cref="LidarrProxy"/> tells the client the proxy endpoint is available.
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
        /// Gets or sets the Music Grabber base URL (empty when unset), so clients can offer the
        /// "grab a track" flow with zero per-device setup.
        /// </summary>
        public string MusicGrabberUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Music Grabber API key for the client's direct grab calls. This is a
        /// CLIENT-grade credential by design — the Cadence web app has always shipped it in its
        /// public JS bundle — so serving it here, to authenticated Jellyfin users only, is strictly
        /// tighter than the status quo (unlike the Lidarr key, which stays server-side).
        /// </summary>
        public string MusicGrabberApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Builds the client-facing config from the plugin configuration, deliberately omitting the
        /// Lidarr API key and only surfacing a boolean for whether the proxy is usable.
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
                MusicGrabberApiKey = config.MusicGrabberApiKey ?? string.Empty,
            };
        }
    }
}
