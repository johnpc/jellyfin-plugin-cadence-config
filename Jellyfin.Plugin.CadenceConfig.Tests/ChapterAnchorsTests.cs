using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class ChapterAnchorsTests
    {
        // Hogfather in miniature: chapters at 0, 30, 42, 170, 400, 900 (total 950). Chunks 1, 4, 5 embedded;
        // 2, 3 (credits, theme) and 6 (end credits) are silent and missing. Lengths = chapter lengths.
        private static readonly double[] Marks = { 0, 30, 42, 170, 400, 900 };

        private static Timeline Raw() => new()
        {
            AudioFiles = new[] { "a/00001-00001.mp4", "a/00001-00004.mp4", "a/00001-00005.mp4" },
            AudioOffsets = new[] { 0.0, 30.0, 260.0 },
            Total = 760,
            Clips = new List<ReadAlongClip>
            {
                new() { Start = 1, End = 29, Chapter = 0, Fragment = "ded" },
                new() { Start = 31, End = 40, Chapter = 1, Fragment = "ch1-first" },
                new() { Start = 261, End = 270, Chapter = 2, Fragment = "ch2-first" },
            },
        };

        [Fact]
        public void Reconcile_PinsEachChunkToItsChapterMarker_IncludingAfterSilentHoles()
        {
            var t = ChapterAnchors.Reconcile(Raw(), 950, Marks)!;

            t.Should().NotBeNull();
            t.AudioOffsets.Should().Equal(0, 170, 400);
            t.Total.Should().Be(950);
            t.Clips.Select(c => (c.Fragment, c.Start)).Should().Equal(("ded", 1), ("ch1-first", 171), ("ch2-first", 401));
            OverlayTimeline.Check(t, 950).Should().Be(ReadAlongStatus.Ready);
        }

        [Fact]
        public void Walk_LeadingSilentChunks_AndChaptersCutInPieces()
        {
            // The Institute's shape: chapters 0, 46, 55, 132, 5000, 12300 (total 14000). Chunks 1 and 2 (intro,
            // credits) are silent and missing; chapter 4 (132..5000) is one chunk; chapter 5 (5000..12300) is
            // two pieces of 4000 + 3300; the last chapter is one chunk.
            var marks = new[] { 0.0, 46, 55, 132, 5000, 12300 };
            var present = new[] { 3, 4, 5, 6, 7 };
            var probed = new[] { 77.0, 4868, 4000, 3300, 1700 };

            var starts = ChapterAnchors.Walk(present, probed, marks, 14000);

            starts.Should().Equal(55, 132, 5000, 9000, 12300);
        }

        [Fact]
        public void Walk_ReturnsNull_WhenPiecesDoNotAddUpToTheChapter()
        {
            var marks = new[] { 0.0, 100, 300, 500 };
            ChapterAnchors.Walk(new[] { 1, 2, 3 }, new[] { 100.0, 150, 200 }, marks, 700).Should().BeNull();
        }

        [Fact]
        public void Reconcile_ReturnsNull_WithoutUsableMarkersOrNumbers()
        {
            ChapterAnchors.Reconcile(Raw(), 950, new double[0]).Should().BeNull();
            var unnumbered = Raw();
            unnumbered.AudioFiles = new[] { "intro.mp4", "body.mp4", "end.mp4" };
            ChapterAnchors.Reconcile(unnumbered, 950, Marks).Should().BeNull();
        }

        [Fact]
        public void Reconcile_ToleratesSmallProbeDrift()
        {
            var raw = Raw();
            raw.AudioOffsets = new[] { 0.0, 31.2, 262.0 };
            raw.Total = 761.5;
            ChapterAnchors.Reconcile(raw, 950, Marks).Should().NotBeNull();
        }
    }
}
