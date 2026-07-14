using Jellyfin.Plugin.CadenceConfig.Configuration;

namespace Jellyfin.Plugin.CadenceConfig.Lidarr
{
    /// <summary>
    /// The outcome of planning a proxied Lidarr request: either an HTTP status to return
    /// immediately (refused / unavailable), or an upstream target to forward to. Pure + free of any
    /// HTTP machinery so the whole decision is unit-testable; the controller only executes the plan.
    /// </summary>
    public sealed class LidarrProxyPlan
    {
        private LidarrProxyPlan(bool forward, int statusCode, string? targetUrl, string? apiKey)
        {
            Forward = forward;
            StatusCode = statusCode;
            TargetUrl = targetUrl;
            ApiKey = apiKey;
        }

        /// <summary>
        /// Gets a value indicating whether the request should be forwarded to Lidarr. When false,
        /// the controller returns <see cref="StatusCode"/>.
        /// </summary>
        public bool Forward { get; }

        /// <summary>
        /// Gets the HTTP status to return when <see cref="Forward"/> is false (0 when forwarding).
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Gets the absolute upstream Lidarr URL to forward to (null when not forwarding).
        /// </summary>
        public string? TargetUrl { get; }

        /// <summary>
        /// Gets the Lidarr API key to inject server-side (null when not forwarding). Never leaves
        /// the server — the controller attaches it to the outbound request only.
        /// </summary>
        public string? ApiKey { get; }

        /// <summary>
        /// Plans a proxied request: enforce the allowlist (403 if not), require a configured +
        /// valid Lidarr URL and key (503 if not), else forward to the mapped upstream URL.
        /// </summary>
        /// <param name="method">The HTTP method (GET/POST).</param>
        /// <param name="relativePath">The sub-path (with query string) after the proxy prefix.</param>
        /// <param name="config">The plugin configuration holding the Lidarr URL + key.</param>
        /// <returns>The plan to execute.</returns>
        public static LidarrProxyPlan Create(string? method, string? relativePath, PluginConfiguration config)
        {
            if (!LidarrProxyPolicy.IsAllowed(relativePath, method))
            {
                return Refused(403);
            }

            if (!LidarrUpstream.IsValidBaseUrl(config.LidarrUrl) || string.IsNullOrWhiteSpace(config.LidarrApiKey))
            {
                return Refused(503);
            }

            var target = LidarrUpstream.Build(config.LidarrUrl, relativePath);
            return target == null ? Refused(503) : new LidarrProxyPlan(true, 0, target, config.LidarrApiKey);
        }

        private static LidarrProxyPlan Refused(int statusCode) =>
            new LidarrProxyPlan(false, statusCode, null, null);
    }
}
