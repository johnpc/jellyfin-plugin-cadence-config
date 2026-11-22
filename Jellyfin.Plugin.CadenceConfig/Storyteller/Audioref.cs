using System;
using System.Globalization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Parses a guided-navigation <c>audioref</c> — <c>path/to/chunk.mp4#t=12.34,15.67</c> (W3C media
    /// fragment) — into the chunk path and clip bounds. Also parses a bare <c>t=12.3</c> fragment as
    /// used in Readium audio locators.
    /// </summary>
    public static class Audioref
    {
        /// <summary>Parses an audioref; null when it has no usable time fragment.</summary>
        /// <param name="audioref">The reference.</param>
        /// <returns>The clip, or null.</returns>
        public static AudioClip? Parse(string? audioref)
        {
            if (string.IsNullOrWhiteSpace(audioref))
            {
                return null;
            }

            var hash = audioref.IndexOf('#', StringComparison.Ordinal);
            if (hash < 0)
            {
                return null;
            }

            var times = ParseTimeFragment(audioref[(hash + 1)..]);
            return times == null ? null : new AudioClip(audioref[..hash], times.Value.Start, times.Value.End);
        }

        /// <summary>Parses <c>t=a,b</c> / <c>t=a</c> / <c>t=,b</c>; null when malformed.</summary>
        /// <param name="fragment">The fragment without the leading '#'.</param>
        /// <returns>Start and optional end.</returns>
        public static (double Start, double? End)? ParseTimeFragment(string? fragment)
        {
            if (fragment == null || !fragment.StartsWith("t=", StringComparison.Ordinal))
            {
                return null;
            }

            var parts = fragment[2..].Split(',');
            var start = parts[0].Length == 0 ? 0 : ParseSeconds(parts[0]);
            double? end = parts.Length > 1 && parts[1].Length > 0 ? ParseSeconds(parts[1]) : null;
            if (start == null || (parts.Length > 1 && parts[1].Length > 0 && end == null))
            {
                return null;
            }

            return (start.Value, end);
        }

        private static double? ParseSeconds(string text)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                return seconds;
            }

            // npt hh:mm:ss.fff form
            var pieces = text.Split(':');
            double total = 0;
            foreach (var piece in pieces)
            {
                if (!double.TryParse(piece, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                {
                    return null;
                }

                total = (total * 60) + v;
            }

            return total;
        }
    }
}
