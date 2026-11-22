using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Builds a <see cref="Timeline"/> from the readaloud manifest, one guided-navigation document per
    /// chapter, and the probed duration of every audio chunk. Chunks are ordered by natural href order
    /// (00001-00002 before 00001-00010) — the same order Storyteller itself concatenates them in — so
    /// global time = chunk offset + clip time.
    /// </summary>
    public static class OverlayTimeline
    {
        /// <summary>Storyteller's own tolerance for "the aligned audio is the audiobook".</summary>
        public const double MismatchToleranceSeconds = 3;

        /// <summary>Builds the timeline.</summary>
        /// <param name="manifest">The readaloud manifest.</param>
        /// <param name="guides">Guided-navigation docs keyed by chapter href (missing chapters are skipped).</param>
        /// <param name="chunkDurations">Chunk durations in seconds keyed by root-relative audio path.</param>
        /// <returns>The timeline.</returns>
        public static Timeline Build(
            ReadiumManifest manifest,
            IReadOnlyDictionary<string, GuidedNavigationDocument> guides,
            IReadOnlyDictionary<string, double> chunkDurations)
        {
            var chapters = manifest.ReadingOrder
                .Select(l => new ReadAlongChapter { Href = l.Href.TrimStart('/'), Title = l.Title })
                .ToList();

            var raw = new List<(string Audio, double Start, double End, int Chapter, string Fragment)>();
            for (var i = 0; i < chapters.Count; i++)
            {
                if (!guides.TryGetValue(chapters[i].Href, out var guide))
                {
                    continue;
                }

                foreach (var node in Flatten(guide.Guided))
                {
                    var clip = Audioref.Parse(node.Audioref);
                    var fragment = FragmentOf(node.Textref);
                    if (clip == null || fragment == null)
                    {
                        continue;
                    }

                    var audio = StorytellerUrls.ResolveAgainst(chapters[i].Href, clip.Path);
                    raw.Add((audio, clip.Start, clip.End ?? clip.Start, i, fragment));
                }
            }

            var files = raw.Select(r => r.Audio).Distinct().OrderBy(f => f, NaturalOrder.Instance).ToList();
            var offsets = new List<double>(files.Count);
            double running = 0;
            foreach (var file in files)
            {
                offsets.Add(running);
                running += chunkDurations.TryGetValue(file, out var d)
                    ? d
                    : raw.Where(r => r.Audio == file).Select(r => r.End).DefaultIfEmpty(0).Max();
            }

            var offsetByFile = files.Zip(offsets).ToDictionary(x => x.First, x => x.Second);
            var clips = raw
                .Select(r => new ReadAlongClip
                {
                    Start = offsetByFile[r.Audio] + r.Start,
                    End = offsetByFile[r.Audio] + r.End,
                    Chapter = r.Chapter,
                    Fragment = r.Fragment,
                })
                .OrderBy(c => c.Start)
                .ToList();

            return new Timeline
            {
                Chapters = chapters,
                AudioFiles = files,
                AudioOffsets = offsets,
                Total = running,
                Clips = clips,
            };
        }

        /// <summary>Whether the aligned audio and the Jellyfin book are the same recording.</summary>
        /// <param name="timeline">The built timeline.</param>
        /// <param name="partsTotalSeconds">The Jellyfin book's total length.</param>
        /// <returns><see cref="ReadAlongStatus.Ready"/>, <see cref="ReadAlongStatus.Mismatch"/>, or <see cref="ReadAlongStatus.Empty"/> when there is no timing at all.</returns>
        public static ReadAlongStatus Check(Timeline timeline, double partsTotalSeconds)
        {
            if (timeline.Clips.Count == 0)
            {
                return ReadAlongStatus.Empty;
            }

            return Math.Abs(timeline.Total - partsTotalSeconds) <= MismatchToleranceSeconds
                ? ReadAlongStatus.Ready
                : ReadAlongStatus.Mismatch;
        }

        /// <summary>Depth-first flatten of guided nodes.</summary>
        /// <param name="nodes">The nodes.</param>
        /// <returns>Every node, parents before children.</returns>
        public static IEnumerable<GuidedNode> Flatten(IEnumerable<GuidedNode>? nodes)
        {
            foreach (var node in nodes ?? Array.Empty<GuidedNode>())
            {
                yield return node;
                foreach (var child in Flatten(node.Children))
                {
                    yield return child;
                }
            }
        }

        /// <summary>The element id after '#' in a textref, or null.</summary>
        /// <param name="textref">The reference.</param>
        /// <returns>The fragment id.</returns>
        public static string? FragmentOf(string? textref)
        {
            if (textref == null)
            {
                return null;
            }

            var hash = textref.IndexOf('#', StringComparison.Ordinal);
            return hash >= 0 && hash < textref.Length - 1 ? textref[(hash + 1)..] : null;
        }
    }
}
