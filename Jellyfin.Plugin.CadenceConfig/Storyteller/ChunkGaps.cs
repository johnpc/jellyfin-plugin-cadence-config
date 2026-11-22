using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Reconciles a readaloud timeline with the real audiobook length. Storyteller only embeds the
    /// transcoded chunks that carry at least one aligned sentence, so a chunk with no narration (a
    /// 20 s sliver at a chapter boundary, a music-only outro) is simply absent from the aligned book —
    /// and every clip after it would land that many seconds early against the m4b. Chunks are numbered
    /// (<c>00001-00022.mp4</c>), so a hole in the sequence says WHERE audio is missing; the m4b's total
    /// says HOW MUCH. Found live on 2026-11-21: chunk 22 of 38 missing, 20.21 s.
    /// </summary>
    public static partial class ChunkGaps
    {
        /// <summary>Trailing chunk number of an audio href (<c>…/00001-00022.mp4</c> → 22), or null.</summary>
        /// <param name="file">The audio href.</param>
        /// <returns>The number, or null when the name isn't numbered.</returns>
        public static int? ChunkNumber(string file)
        {
            var m = Numbered().Match(file);
            return m.Success ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
        }

        /// <summary>
        /// Stretches the timeline to the audiobook's real length: missing numbered chunks get the leftover
        /// duration (split evenly) inserted at their position and later clips shift; leftover with no
        /// numbered hole is treated as trailing audio (nothing shifts). A timeline already within
        /// tolerance, or LONGER than the audiobook, is returned unchanged (the latter stays a mismatch).
        /// </summary>
        /// <param name="raw">The timeline as built from the readaloud.</param>
        /// <param name="partsTotalSeconds">The Jellyfin parts' total length.</param>
        /// <returns>The reconciled timeline (or <paramref name="raw"/> itself).</returns>
        public static Timeline Reconcile(Timeline raw, double partsTotalSeconds)
        {
            var leftover = partsTotalSeconds - raw.Total;
            if (leftover <= OverlayTimeline.MismatchToleranceSeconds || raw.AudioFiles.Count == 0)
            {
                return raw;
            }

            var numbers = raw.AudioFiles.Select(ChunkNumber).ToList();
            var shifts = numbers.All(n => n != null) ? ShiftsForHoles(numbers.Select(n => n!.Value).ToList(), leftover) : new double[raw.AudioFiles.Count];
            var offsets = raw.AudioOffsets.Select((o, i) => o + shifts[i]).ToList();
            var clips = raw.Clips
                .Select(c => Shifted(c, shifts[FileIndexOf(raw.AudioOffsets, c.Start)]))
                .ToList();

            return new Timeline
            {
                BookUuid = raw.BookUuid,
                Fingerprint = raw.Fingerprint,
                Chapters = raw.Chapters,
                AudioFiles = raw.AudioFiles,
                AudioOffsets = offsets,
                Total = partsTotalSeconds,
                Clips = clips,
            };
        }

        /// <summary>Per-file shift: the leftover split evenly over the holes in the numbering, accumulated.</summary>
        /// <param name="numbers">Chunk numbers in timeline order.</param>
        /// <param name="leftover">Seconds the audiobook is longer than the readaloud.</param>
        /// <returns>A shift per file (0 for files before the first hole).</returns>
        public static double[] ShiftsForHoles(IReadOnlyList<int> numbers, double leftover)
        {
            var holes = 0;
            for (var i = 1; i < numbers.Count; i++)
            {
                holes += Math.Max(0, numbers[i] - numbers[i - 1] - 1);
            }

            var shifts = new double[numbers.Count];
            if (holes == 0)
            {
                return shifts;
            }

            var perHole = leftover / holes;
            double running = 0;
            for (var i = 1; i < numbers.Count; i++)
            {
                running += Math.Max(0, numbers[i] - numbers[i - 1] - 1) * perHole;
                shifts[i] = running;
            }

            return shifts;
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

        private static ReadAlongClip Shifted(ReadAlongClip c, double by) => new()
        {
            Start = c.Start + by,
            End = c.End + by,
            Chapter = c.Chapter,
            Fragment = c.Fragment,
        };

        [GeneratedRegex(@"-(\d+)\.[A-Za-z0-9]+$")]
        private static partial Regex Numbered();
    }
}
