using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Pins the readaloud's audio chunks to the audiobook's own chapter markers. Storyteller splits a
    /// book's audio at its embedded chapter marks, so chunk <c>00001-000NN</c> IS chapter NN of the m4b
    /// and its exact start is Jellyfin's marker — including the chunks Storyteller never embedded
    /// (credits, theme music: no narration). Found live on 2026-11-22 with Hogfather: 114 markers, 114
    /// chunks, 3 silent ones missing (two in the middle, one at the end); splitting the leftover evenly
    /// over the two middle holes put the whole book 48 s late. Markers make the layout exact.
    /// </summary>
    public static class ChapterAnchors
    {
        /// <summary>How far a probed chunk length may differ from its chapter's length and still count as "the same chunk".</summary>
        public const double ToleranceSeconds = 2.0;

        /// <summary>The share of present chunks that must match their chapter length for the anchoring to be trusted.</summary>
        public const double RequiredAgreement = 0.9;

        /// <summary>
        /// Re-lays the timeline on the chapter markers, or returns null when the chunks don't line up
        /// with the chapters (no markers, fewer markers than chunks, or lengths disagree) so the caller
        /// can fall back to <see cref="ChunkGaps.Reconcile"/>.
        /// </summary>
        /// <param name="raw">The timeline as built from the readaloud.</param>
        /// <param name="partsTotalSeconds">The audiobook's total length.</param>
        /// <param name="chapterStarts">Chapter start times in global seconds, ascending.</param>
        /// <returns>The anchored timeline, or null.</returns>
        public static Timeline? Reconcile(Timeline raw, double partsTotalSeconds, IReadOnlyList<double> chapterStarts)
        {
            var numbers = raw.AudioFiles.Select(ChunkGaps.ChunkNumber).ToList();
            if (chapterStarts.Count < 3 || numbers.Count == 0 || numbers.Any(n => n == null || n < 1 || n > chapterStarts.Count))
            {
                return null;
            }

            var probed = ProbedDurations(raw);
            var agree = 0;
            for (var i = 0; i < numbers.Count; i++)
            {
                var k = numbers[i]!.Value;
                var expected = (k < chapterStarts.Count ? chapterStarts[k] : partsTotalSeconds) - chapterStarts[k - 1];
                if (Math.Abs(probed[i] - expected) <= ToleranceSeconds)
                {
                    agree++;
                }
            }

            if (agree < numbers.Count * RequiredAgreement)
            {
                return null;
            }

            var offsets = numbers.Select(n => chapterStarts[n!.Value - 1]).ToList();
            var shifts = offsets.Select((o, i) => o - raw.AudioOffsets[i]).ToList();
            return new Timeline
            {
                BookUuid = raw.BookUuid,
                Fingerprint = raw.Fingerprint,
                Chapters = raw.Chapters,
                AudioFiles = raw.AudioFiles,
                AudioOffsets = offsets,
                Total = partsTotalSeconds,
                Clips = raw.Clips.Select(c => Shift(c, shifts[FileIndexOf(raw.AudioOffsets, c.Start)])).OrderBy(c => c.Start).ToList(),
            };
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
