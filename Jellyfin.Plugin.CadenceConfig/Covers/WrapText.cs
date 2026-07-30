using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Jellyfin.Plugin.CadenceConfig.Covers
{
    /// <summary>
    /// Greedy word-wrap for the name-based cover title: packs words onto lines up to a pixel width,
    /// capped at a max line count. Once on the last allowed line, all remaining words stay on it (a
    /// long name is never dropped, just tight). The width measurer is injected so the core is
    /// unit-testable without a real font.
    /// </summary>
    public static class WrapText
    {
        /// <summary>Wrap using an <see cref="SKPaint"/> to measure text width.</summary>
        /// <param name="text">The text to wrap.</param>
        /// <param name="paint">Paint whose TextSize/Typeface drive measurement.</param>
        /// <param name="maxWidth">Max line width in pixels.</param>
        /// <param name="maxLines">Max number of lines.</param>
        /// <returns>The wrapped lines.</returns>
        public static IReadOnlyList<string> Wrap(string text, SKPaint paint, float maxWidth, int maxLines) =>
            Wrap(text, s => paint.MeasureText(s), maxWidth, maxLines);

        /// <summary>Wrap with an injected width measurer (pure core — no font needed for tests).</summary>
        /// <param name="text">The text to wrap.</param>
        /// <param name="measure">Returns the pixel width of a string.</param>
        /// <param name="maxWidth">Max line width in pixels.</param>
        /// <param name="maxLines">Max number of lines.</param>
        /// <returns>The wrapped lines (at least one; never empty for non-blank input).</returns>
        public static IReadOnlyList<string> Wrap(
            string text,
            Func<string, float> measure,
            float maxWidth,
            int maxLines)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var current = string.Empty;
            foreach (var word in words)
            {
                // On the final allowed line, keep appending — never start another.
                var onLastLine = lines.Count == maxLines - 1;
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (!onLastLine && current.Length > 0 && measure(candidate) > maxWidth)
                {
                    lines.Add(current);
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }

            if (current.Length > 0)
            {
                lines.Add(current);
            }

            return lines.Count == 0 ? new List<string> { text } : lines;
        }
    }
}
