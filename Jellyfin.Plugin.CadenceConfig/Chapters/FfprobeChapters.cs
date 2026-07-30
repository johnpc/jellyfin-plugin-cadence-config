using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Jellyfin.Plugin.CadenceConfig.Chapters
{
    /// <summary>
    /// Pure parser for the JSON emitted by <c>ffprobe -print_format json -show_chapters</c>. Jellyfin's
    /// own <c>IMediaEncoder.GetMediaInfo</c> does NOT populate chapters for audio files (verified on the
    /// live server: a 25-chapter m4b returns none), so we shell out to ffprobe directly and parse its
    /// output here. Kept free of any I/O so it's fully unit testable against captured ffprobe output.
    /// </summary>
    public static class FfprobeChapters
    {
        /// <summary>
        /// Parse ffprobe <c>-show_chapters</c> JSON into the client shape. Each chapter carries a
        /// <c>start_time</c> in SECONDS (already the unit ffprobe uses) and an optional <c>tags.title</c>;
        /// unnamed chapters fall back to "Chapter N". Ordered by start. Returns an empty list for absent
        /// or DEGENERATE chapter data (no chapters, or a single/all-zero marker some encoders emit),
        /// matching the "real list or nothing" contract the client relies on. Never throws on malformed
        /// JSON — returns empty so a bad probe can't 500 the endpoint.
        /// </summary>
        /// <param name="json">Raw ffprobe JSON output.</param>
        /// <returns>The client-facing chapters, or an empty list.</returns>
        public static IReadOnlyList<AudiobookChapter> Parse(string? json)
        {
            var empty = new List<AudiobookChapter>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return empty;
            }

            List<(double Start, string? Title)> raw;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("chapters", out var chapters)
                    || chapters.ValueKind != JsonValueKind.Array)
                {
                    return empty;
                }

                raw = chapters.EnumerateArray().Select(ReadChapter).ToList();
            }
            catch (JsonException)
            {
                return empty;
            }

            var ordered = raw.OrderBy(c => c.Start).ToList();
            if (ordered.Count == 0
                || (ordered.Count < 2 && ordered[0].Start <= 0)
                || ordered.All(c => c.Start <= 0))
            {
                return empty;
            }

            return ordered
                .Select((c, i) => new AudiobookChapter
                {
                    Name = string.IsNullOrWhiteSpace(c.Title)
                        ? "Chapter " + (i + 1).ToString(CultureInfo.InvariantCulture)
                        : c.Title!,
                    Start = c.Start,
                })
                .ToList();
        }

        private static (double Start, string? Title) ReadChapter(JsonElement el)
        {
            double start = 0;
            if (el.TryGetProperty("start_time", out var st)
                && st.ValueKind == JsonValueKind.String
                && double.TryParse(st.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                start = parsed;
            }

            string? title = null;
            if (el.TryGetProperty("tags", out var tags)
                && tags.ValueKind == JsonValueKind.Object
                && tags.TryGetProperty("title", out var t)
                && t.ValueKind == JsonValueKind.String)
            {
                title = t.GetString();
            }

            return (start, title);
        }
    }
}
