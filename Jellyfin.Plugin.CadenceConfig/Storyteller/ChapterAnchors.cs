using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Pins the readaloud's audio chunks to the audiobook's own chapter markers. Storyteller cuts a book's
    /// audio at its embedded chapter marks, splits a long chapter into roughly two-hour pieces, and only
    /// embeds the pieces that carry narration (credits and theme music are left out). Walking the chapters
    /// in order with a cursor over chunk numbers: a chunk whose probed length equals the chapter's IS that
    /// chapter; a missing chunk number stands for a silent chapter; several chunks whose lengths add up to
    /// the chapter are its pieces. Every start is then exact. Found live on 2026-11-22: Hogfather (114
    /// chapters, 3 silent) and The Institute (13 chapters, 2 silent at the start, 4 split into pieces).
    /// </summary>
    public static class ChapterAnchors
    {
        /// <summary>How far probed lengths may differ from the chapter markers and still be trusted.</summary>
        public const double ToleranceSeconds = 2.5;

        /// <summary>
        /// Re-lays the timeline on the chapter markers, or returns null when the chunks can't be walked onto
        /// the chapters (no markers, unnumbered files, lengths that don't add up) so the caller falls back.
        /// </summary>
        /// <param name="raw">The timeline as built from the readaloud.</param>
        /// <param name="partsTotalSeconds">The audiobook's total length.</param>
        /// <param name="chapterStarts">Chapter start times in global seconds, ascending.</param>
        /// <returns>The anchored timeline, or null.</returns>
        public static Timeline? Reconcile(Timeline raw, double partsTotalSeconds, IReadOnlyList<double> chapterStarts)
        {
            var numbers = raw.AudioFiles.Select(ChunkGaps.ChunkNumber).ToList();
            if (chapterStarts.Count < 2 || numbers.Count == 0 || numbers.Any(n => n == null || n < 1))
            {
                return null;
            }

            var starts = Walk(numbers.Select(n => n!.Value).ToList(), ProbedDurations(raw), chapterStarts, partsTotalSeconds);
            if (starts == null)
            {
                return null;
            }

            var shifts = starts.Select((s, i) => s - raw.AudioOffsets[i]).ToList();
            return new Timeline
            {
                BookUuid = raw.BookUuid,
                Fingerprint = raw.Fingerprint,
                Chapters = raw.Chapters,
                AudioFiles = raw.AudioFiles,
                AudioOffsets = starts,
                Total = partsTotalSeconds,
                Clips = raw.Clips.Select(c => Shift(c, shifts[FileIndexOf(raw.AudioOffsets, c.Start)])).OrderBy(c => c.Start).ToList(),
            };
        }

        /// <summary>
        /// Assigns a start to every PRESENT chunk by walking the chapters with a cursor over chunk numbers
        /// 1..N. Returns null when the lengths don't add up.
        /// </summary>
        /// <param name="present">Chunk numbers in timeline order.</param>
        /// <param name="probed">Their probed lengths, same order.</param>
        /// <param name="marks">Chapter starts.</param>
        /// <param name="total">The audiobook's total length.</param>
        /// <returns>A start per present chunk, or null.</returns>
        public static List<double>? Walk(IReadOnlyList<int> present, IReadOnlyList<double> probed, IReadOnlyList<double> marks, double total)
        {
            var byNumber = present.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
            var starts = new double[present.Count];
            var number = 1;
            for (var chapter = 0; chapter < marks.Count; chapter++)
            {
                var chapterEnd = chapter + 1 < marks.Count ? marks[chapter + 1] : total;
                var length = chapterEnd - marks[chapter];
                if (!byNumber.TryGetValue(number, out var index))
                {
                    number++; // a silent chapter Storyteller never embedded
                    continue;
                }

                double consumed = 0;
                while (true)
                {
                    starts[index] = marks[chapter] + consumed;
                    consumed += probed[index];
                    number++;
                    if (Math.Abs(consumed - length) <= ToleranceSeconds)
                    {
                        break;
                    }

                    if (consumed > length + ToleranceSeconds || !byNumber.TryGetValue(number, out index))
                    {
                        return null; // the pieces don't add up to this chapter
                    }
                }
            }

            return number - 1 >= present.Max() ? starts.ToList() : null;
        }

        private static List<double> ProbedDurations(Timeline raw)
        {
            var d = new List<double>(raw.AudioOffsets.Count);
            for (var i = 0; i < raw.AudioOffsets.Count; i++)
            {
                var end = i + 1 < raw.AudioOffsets.Count ? raw.AudioOffsets[i + 1] : raw.Total;
                d.Add(end - raw.AudioOffsets[i]);
            }

            return d;
        }

        private static int FileIndexOf(IReadOnlyList<double> offsets, double start)
        {
            var index = 0;
            for (var i = 0; i < offsets.Count; i++)
            {
                if (offsets[i] <= start + 1e-6)
                {
                    index = i;
                }
            }

            return index;
        }

        private static ReadAlongClip Shift(ReadAlongClip c, double by) => new()
        {
            Start = c.Start + by,
            End = c.End + by,
            Chapter = c.Chapter,
            Fragment = c.Fragment,
        };
    }
}
