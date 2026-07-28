using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.CadenceConfig.Chapters
{
    /// <summary>
    /// Pure mapping from Jellyfin's ffprobe <see cref="ChapterInfo"/> markers to the client-facing
    /// <see cref="AudiobookChapter"/> list. Kept free of any Jellyfin services so it's fully unit
    /// testable — the controller/service just feed it the probed markers.
    /// </summary>
    public static class ChapterMapper
    {
        private const long TicksPerSecond = 10_000_000;

        /// <summary>
        /// Convert probed chapter markers to the client shape: ticks → seconds, a "Chapter N" fallback
        /// for unnamed markers, ordered by start. Returns an empty list when the markers are absent or
        /// DEGENERATE — some encoders emit a single zero-length marker (or all markers at 0s), which is
        /// not a real chapter list and would just clutter the UI; treat that as "no chapters".
        /// </summary>
        /// <param name="markers">The raw ffprobe chapter markers (may be null).</param>
        /// <returns>The client-facing chapters, or an empty list when there are none worth showing.</returns>
        public static IReadOnlyList<AudiobookChapter> ToClientChapters(IReadOnlyList<ChapterInfo>? markers)
        {
            if (markers is null || markers.Count == 0)
            {
                return new List<AudiobookChapter>();
            }

            var ordered = markers.OrderBy(m => m.StartPositionTicks).ToList();

            // Degenerate: one marker, or every marker sitting at 0 — not a usable chapter list.
            if (ordered.Count < 2 && ordered[0].StartPositionTicks <= 0)
            {
                return new List<AudiobookChapter>();
            }

            if (ordered.All(m => m.StartPositionTicks <= 0))
            {
                return new List<AudiobookChapter>();
            }

            return ordered
                .Select((m, i) => new AudiobookChapter
                {
                    Name = string.IsNullOrWhiteSpace(m.Name)
                        ? "Chapter " + (i + 1).ToString(CultureInfo.InvariantCulture)
                        : m.Name!,
                    Start = (double)m.StartPositionTicks / TicksPerSecond,
                })
                .ToList();
        }
    }
}
