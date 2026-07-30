using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Grab
{
    /// <summary>
    /// Pure choice of the best Music Grabber result for a wanted "artist - title": require a loose
    /// title-word overlap (so a wildly-wrong hit is rejected), then prefer the highest quality_score
    /// (the grabber ranks lossless/Soulseek above YouTube). No I/O — unit-testable.
    /// </summary>
    public static class GrabPick
    {
        /// <summary>
        /// The best result for the wanted track, or null if none plausibly matches. A candidate must
        /// share at least half of the wanted TITLE's significant words (ignoring parenthetical noise
        /// like "(Remastered)") with its own title; among those, the highest quality score wins.
        /// </summary>
        /// <param name="results">The search results.</param>
        /// <param name="wantedTitle">The Deezer track title.</param>
        /// <param name="wantedArtist">The Deezer artist name.</param>
        /// <returns>The chosen result, or null.</returns>
        public static GrabResult? Best(IReadOnlyList<GrabResult> results, string wantedTitle, string? wantedArtist)
        {
            ArgumentNullException.ThrowIfNull(results);
            var wantWords = SignificantWords(wantedTitle);
            if (wantWords.Count == 0)
            {
                return null;
            }

            GrabResult? best = null;
            var bestScore = int.MinValue;
            foreach (var r in results)
            {
                var hay = Normalize((r.Title ?? string.Empty) + " " + (r.Artist ?? r.Channel ?? string.Empty));
                var hit = wantWords.Count(w => hay.Contains(w, StringComparison.Ordinal));
                if (hit * 2 < wantWords.Count)
                {
                    continue; // fewer than half the title words present — not this track
                }

                if (r.QualityScore > bestScore)
                {
                    best = r;
                    bestScore = r.QualityScore;
                }
            }

            return best;
        }

        /// <summary>Lower-cased alphanumeric words of 2+ chars, with parenthetical/bracketed noise
        /// stripped ("(Remastered 2015)", "[Live]") so match focuses on the real title.</summary>
        private static List<string> SignificantWords(string? title)
        {
            var cleaned = Normalize(StripBrackets(title ?? string.Empty));
            return cleaned
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 2)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static string StripBrackets(string s)
        {
            var chars = s.ToCharArray();
            var depth = 0;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in chars)
            {
                if (c is '(' or '[')
                {
                    depth++;
                }
                else if (c is ')' or ']')
                {
                    depth = Math.Max(0, depth - 1);
                }
                else if (depth == 0)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static string Normalize(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s)
            {
                sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
            }

            return sb.ToString();
        }
    }
}
