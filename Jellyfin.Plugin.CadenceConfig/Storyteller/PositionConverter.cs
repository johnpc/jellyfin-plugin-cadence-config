using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Converts between global audio seconds and Readium locators over a built <see cref="Timeline"/>.
    /// Pure. Seconds → a TEXT locator (the shape Storyteller's reader saves, so it opens at that
    /// sentence); locator → seconds accepts text locators (fragment, then progression), audio locators
    /// (<c>t=</c> on a chunk), and finally falls back to totalProgression.
    /// </summary>
    public static class PositionConverter
    {
        private const string XhtmlType = "application/xhtml+xml";

        /// <summary>Index of the clip playing at <c>seconds</c>: the last clip whose start ≤ seconds (or 0).</summary>
        /// <param name="clips">Clips sorted by start.</param>
        /// <param name="seconds">Global seconds.</param>
        /// <returns>The index, or -1 for no clips.</returns>
        public static int ClipAt(IReadOnlyList<ReadAlongClip> clips, double seconds)
        {
            if (clips.Count == 0)
            {
                return -1;
            }

            int lo = 0, hi = clips.Count - 1, best = 0;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                if (clips[mid].Start <= seconds)
                {
                    best = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return best;
        }

        /// <summary>A text locator for a global position; null when the timeline has no clips.</summary>
        /// <param name="timeline">The timeline.</param>
        /// <param name="seconds">Global seconds.</param>
        /// <param name="bookTotalSeconds">The book length used for totalProgression.</param>
        /// <returns>The locator.</returns>
        public static ReadiumLocator? ToLocator(Timeline timeline, double seconds, double bookTotalSeconds)
        {
            var index = ClipAt(timeline.Clips, seconds);
            if (index < 0)
            {
                return null;
            }

            var clip = timeline.Clips[index];
            var chapter = timeline.Chapters[clip.Chapter];
            var inChapter = timeline.Clips.Where(c => c.Chapter == clip.Chapter).ToList();
            var position = inChapter.FindIndex(c => ReferenceEquals(c, clip));
            return new ReadiumLocator
            {
                Href = chapter.Href,
                Type = XhtmlType,
                Title = chapter.Title,
                Locations = new ReadiumLocations
                {
                    Fragments = new[] { clip.Fragment },
                    Progression = inChapter.Count <= 1 ? 0 : (double)position / inChapter.Count,
                    TotalProgression = bookTotalSeconds > 0 ? Math.Clamp(seconds / bookTotalSeconds, 0, 1) : 0,
                },
            };
        }

        /// <summary>Global seconds for a Storyteller locator; null when nothing in it can be placed.</summary>
        /// <param name="timeline">The timeline.</param>
        /// <param name="locator">The locator.</param>
        /// <param name="bookTotalSeconds">The book length used for the totalProgression fallback.</param>
        /// <returns>Global seconds, or null.</returns>
        public static double? ToSeconds(Timeline timeline, ReadiumLocator locator, double bookTotalSeconds)
        {
            var href = locator.Href.TrimStart('/');
            var fragment = locator.Locations?.Fragments is { Count: > 0 } fragments ? fragments[0] : null;

            if (locator.Type.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                var file = timeline.AudioFiles.ToList().FindIndex(f => f == href || f.EndsWith("/" + href, StringComparison.Ordinal));
                var time = Audioref.ParseTimeFragment(fragment);
                if (file >= 0 && time != null)
                {
                    return timeline.AudioOffsets[file] + time.Value.Start;
                }
            }

            var chapter = timeline.Chapters.ToList().FindIndex(c => c.Href == href || c.Href.EndsWith("/" + href, StringComparison.Ordinal));
            if (chapter >= 0)
            {
                var inChapter = timeline.Clips.Where(c => c.Chapter == chapter).ToList();
                if (fragment != null)
                {
                    var hit = inChapter.FirstOrDefault(c => c.Fragment == fragment);
                    if (hit != null)
                    {
                        return hit.Start;
                    }
                }

                if (locator.Locations?.Progression is double progression && inChapter.Count > 0)
                {
                    var at = (int)Math.Clamp(Math.Floor(progression * inChapter.Count), 0, inChapter.Count - 1);
                    return inChapter[at].Start;
                }
            }

            if (locator.Locations?.TotalProgression is double total && bookTotalSeconds > 0)
            {
                return Math.Clamp(total, 0, 1) * bookTotalSeconds;
            }

            return null;
        }
    }
}
