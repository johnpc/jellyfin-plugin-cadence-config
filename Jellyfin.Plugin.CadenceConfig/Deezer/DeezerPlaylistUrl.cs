using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>
    /// Parses a Deezer playlist reference (a full share URL or a bare id) to its numeric playlist
    /// id, which the Deezer API reads at <c>api.deezer.com/playlist/{id}</c>. Pure so it is fully
    /// unit-testable. Accepts the forms users actually paste:
    ///   https://www.deezer.com/playlist/908622995
    ///   https://www.deezer.com/en/playlist/908622995?utm=...
    ///   https://deezer.page.link/... is NOT resolved here (needs a redirect) — return null.
    ///   908622995 (a bare id).
    /// </summary>
    public static class DeezerPlaylistUrl
    {
        private static readonly Regex PlaylistId = new(
            @"playlist/(\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex BareId = new(
            @"^\d+$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Extracts the numeric playlist id from a URL or bare id, or null when the input carries no
        /// recognizable Deezer playlist id.
        /// </summary>
        /// <param name="input">A Deezer playlist URL or a bare numeric id.</param>
        /// <returns>The playlist id as a string of digits, or null.</returns>
        public static string? ParseId(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var trimmed = input.Trim();
            if (BareId.IsMatch(trimmed))
            {
                return trimmed;
            }

            var match = PlaylistId.Match(trimmed);
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
