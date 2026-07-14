using System;
using System.Buffers;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Lidarr
{
    /// <summary>
    /// The curated allowlist for the Lidarr proxy. The injected API key has full write access to the
    /// library manager, so the proxy exposes ONLY the read/add paths the "request music" feature
    /// needs and nothing that could delete the library or change config. This mirrors the former
    /// nginx allowlist (search / profiles / rootfolder / queue / artist / album) so the client is
    /// unchanged. Pure + static so it is fully unit-testable.
    /// </summary>
    public static class LidarrProxyPolicy
    {
        /// <summary>
        /// The first path segment (Lidarr v1 resource) each proxied request must target.
        /// </summary>
        private static readonly HashSet<string> AllowedResources = new(StringComparer.OrdinalIgnoreCase)
        {
            "search",
            "qualityprofile",
            "metadataprofile",
            "rootfolder",
            "queue",
            "artist",
            "album",
        };

        /// <summary>
        /// The characters that terminate the first path segment (path separator, query, fragment).
        /// </summary>
        private static readonly SearchValues<char> SegmentTerminators = SearchValues.Create("/?#");

        /// <summary>
        /// The HTTP methods the proxy permits. Reads (GET) plus the add endpoints (POST). DELETE /
        /// PUT are refused so nothing can remove artists or mutate config through the proxy.
        /// </summary>
        private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "GET",
            "POST",
        };

        /// <summary>
        /// Returns whether a request for the given client sub-path and method is allowed through the
        /// proxy. The sub-path is everything after <c>/Cadence/Lidarr/</c> (e.g. <c>artist</c>,
        /// <c>queue?pageSize=100</c>, <c>artist/123</c>).
        /// </summary>
        /// <param name="subPath">The path after the proxy prefix (query string tolerated).</param>
        /// <param name="method">The HTTP method.</param>
        /// <returns>True when the request may be forwarded to Lidarr.</returns>
        public static bool IsAllowed(string? subPath, string? method)
        {
            if (string.IsNullOrWhiteSpace(subPath) || string.IsNullOrWhiteSpace(method))
            {
                return false;
            }

            if (!AllowedMethods.Contains(method))
            {
                return false;
            }

            return AllowedResources.Contains(FirstSegment(subPath));
        }

        /// <summary>
        /// Extracts the first path segment, ignoring any leading slash, query string, or fragment —
        /// e.g. <c>artist/123?x=1</c> → <c>artist</c>.
        /// </summary>
        /// <param name="subPath">The proxy sub-path.</param>
        /// <returns>The lowercased-comparable first segment (empty when none).</returns>
        public static string FirstSegment(string subPath)
        {
            if (string.IsNullOrEmpty(subPath))
            {
                return string.Empty;
            }

            var trimmed = subPath.TrimStart('/');
            var end = trimmed.AsSpan().IndexOfAny(SegmentTerminators);
            return end < 0 ? trimmed : trimmed.Substring(0, end);
        }
    }
}
