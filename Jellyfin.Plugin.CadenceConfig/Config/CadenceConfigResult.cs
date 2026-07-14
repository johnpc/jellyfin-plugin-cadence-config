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
            };
        }
    }
}
