using System;

namespace Jellyfin.Plugin.CadenceConfig.Lidarr
{
    /// <summary>
    /// Builds the upstream Lidarr URL a proxied request targets. The client calls
    /// <c>/Cadence/Lidarr/&lt;sub&gt;</c>; this maps that to <c>&lt;LidarrUrl&gt;/api/v1/&lt;sub&gt;</c>
    /// (Lidarr's v1 API). Pure so it is unit-testable without a live server.
    /// </summary>
    public static class LidarrUpstream
    {
        /// <summary>
        /// Builds the absolute Lidarr API URL for a proxy sub-path, preserving any query string.
        /// </summary>
        /// <param name="lidarrBaseUrl">The configured Lidarr base URL (trailing slash tolerated).</param>
        /// <param name="subPath">The path after the proxy prefix, e.g. <c>artist</c> or
        /// <c>queue?pageSize=100</c>.</param>
        /// <returns>The absolute upstream URL, or null when the base URL is unset/invalid.</returns>
        public static string? Build(string? lidarrBaseUrl, string? subPath)
        {
            if (string.IsNullOrWhiteSpace(lidarrBaseUrl))
            {
                return null;
            }

            var root = lidarrBaseUrl.TrimEnd('/');
            var sub = (subPath ?? string.Empty).TrimStart('/');
            return $"{root}/api/v1/{sub}";
        }

        /// <summary>
        /// Returns whether the configured base URL is a well-formed absolute http(s) URL. Rejects
        /// anything else so a misconfigured value can't turn into an unexpected request target.
        /// </summary>
        /// <param name="lidarrBaseUrl">The configured Lidarr base URL.</param>
        /// <returns>True when the base URL is a valid http(s) absolute URL.</returns>
        public static bool IsValidBaseUrl(string? lidarrBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(lidarrBaseUrl))
            {
                return false;
            }

            return Uri.TryCreate(lidarrBaseUrl, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Re-joins a proxy sub-path with the incoming query string so both are preserved when
        /// checking the allowlist and building the upstream URL — e.g. <c>queue</c> + <c>?x=1</c>
        /// → <c>queue?x=1</c>.
        /// </summary>
        /// <param name="subPath">The path after the proxy prefix.</param>
        /// <param name="queryString">The incoming query string (leading <c>?</c> included), or null.</param>
        /// <returns>The sub-path with the query string appended.</returns>
        public static string JoinQuery(string? subPath, string? queryString)
        {
            var sub = subPath ?? string.Empty;
            return string.IsNullOrEmpty(queryString) ? sub : sub + queryString;
        }
    }
}
