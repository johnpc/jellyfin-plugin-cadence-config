using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class OverlayTimelineTests
    {
        [Fact]
        public void Build_FlattensNestedNodesOntoTheGlobalTimeline()
        {
            var t = TimelineFixture.Build();

            t.Chapters.Select(c => c.Href).Should().Equal("OEBPS/Text/ch1.xhtml", "OEBPS/Text/ch2.xhtml", "OEBPS/Text/notes.xhtml");
            t.AudioFiles.Should().Equal("OEBPS/Audio/00001-00001.mp4", "OEBPS/Audio/00001-00002.mp4");
            t.AudioOffsets.Should().Equal(0, 20);
            t.Total.Should().Be(30);

            t.Clips.Select(c => (c.Fragment, c.Start, c.End, c.Chapter)).Should().Equal(
                ("h1", 0, 2, 0), ("s1", 2, 6, 0), ("s2", 6, 10, 0), ("s3", 10, 14, 0),
                ("s1", 14, 20, 1), ("s2", 20, 25, 1), ("s3", 25, 30, 1));
        }

        [Fact]
        public void Build_WithoutAProbedDuration_FallsBackToTheChunksLastClipEnd()
        {
            var durations = new Dictionary<string, double> { ["OEBPS/Audio/00001-00002.mp4"] = 10 };
            var t = OverlayTimeline.Build(TimelineFixture.Manifest(), TimelineFixture.Guides(), durations);
            t.AudioOffsets.Should().Equal(0, 20);
            t.Total.Should().Be(30);
        }

        [Fact]
        public void Build_SkipsChaptersWithoutAGuide_AndNodesWithoutClips()
        {
            var t = TimelineFixture.Build();
            t.Clips.Should().NotContain(c => c.Fragment == "noaudio");
            t.Clips.Should().NotContain(c => c.Chapter == 2);
        }

        [Fact]
        public void Check_ReadyWithinThreeSeconds_MismatchBeyond_EmptyWithoutClips()
        {
            var t = TimelineFixture.Build();
            OverlayTimeline.Check(t, 30).Should().Be(ReadAlongStatus.Ready);
            OverlayTimeline.Check(t, 32.9).Should().Be(ReadAlongStatus.Ready);
            OverlayTimeline.Check(t, 34).Should().Be(ReadAlongStatus.Mismatch);
            OverlayTimeline.Check(new Timeline(), 0).Should().Be(ReadAlongStatus.Empty);
        }

        [Fact]
        public void FragmentOf_ReadsTheIdOrNull()
        {
            OverlayTimeline.FragmentOf("ch.xhtml#s1").Should().Be("s1");
            OverlayTimeline.FragmentOf("ch.xhtml#").Should().BeNull();
            OverlayTimeline.FragmentOf("ch.xhtml").Should().BeNull();
            OverlayTimeline.FragmentOf(null).Should().BeNull();
        }
    }
}
