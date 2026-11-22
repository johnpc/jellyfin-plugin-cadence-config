using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class ChapterAnchorsTests
    {
        // Hogfather in miniature: 6 chapters at 0, 30, 42, 170, 400, 900 (total 950). Storyteller embedded chunks
        // 1, 4, 5 only — 2 and 3 (credits, theme) and 6 (end credits) carry no narration. Chunk lengths are the
        // chapter lengths: 30, 230, 500.
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
        public void Reconcile_BeatsTheEvenSplit_WhenATrailingChunkIsAlsoMissing()
        {
            // The even split would put (950-760)=190 s over the two middle holes = 95 each, landing chapter 1 at 125 s
            // instead of 170 s. Markers make it exact.
            var even = ChunkGaps.Reconcile(Raw(), 950);
            even.Clips.First(c => c.Fragment == "ch1-first").Start.Should().NotBe(171);
            ChapterAnchors.Reconcile(Raw(), 950, Marks)!.Clips.First(c => c.Fragment == "ch1-first").Start.Should().Be(171);
        }

        [Fact]
        public void Reconcile_ReturnsNull_WhenChunkLengthsDisagreeWithTheChapters()
        {
            // Silence-split book: chunks don't correspond to the chapter marks.
            var marks = new[] { 0.0, 100, 300, 500, 700, 900 };
            ChapterAnchors.Reconcile(Raw(), 950, marks).Should().BeNull();
        }

        [Fact]
        public void Reconcile_ReturnsNull_WithoutUsableMarkers()
        {
            ChapterAnchors.Reconcile(Raw(), 950, new double[0]).Should().BeNull();
            ChapterAnchors.Reconcile(Raw(), 950, new[] { 0.0, 30 }).Should().BeNull("fewer markers than chunk numbers");
            var unnumbered = Raw();
            unnumbered.AudioFiles = new[] { "intro.mp4", "body.mp4", "end.mp4" };
            ChapterAnchors.Reconcile(unnumbered, 950, Marks).Should().BeNull();
        }

        [Fact]
        public void Reconcile_ToleratesSmallProbeDrift()
        {
            var raw = Raw();
            raw.AudioOffsets = new[] { 0.0, 31.2, 262.0 }; // each chunk ~1 s off its chapter length
            raw.Total = 761.5;
            ChapterAnchors.Reconcile(raw, 950, Marks).Should().NotBeNull();
        }
    }
}
