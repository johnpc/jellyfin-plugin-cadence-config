using System;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class PositionConverterTests
    {
        private static readonly Timeline T = TimelineFixture.Build();

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, 0)]
        [InlineData(3.5, 1)]
        [InlineData(6, 2)]
        [InlineData(14, 4)]
        [InlineData(27, 6)]
        [InlineData(999, 6)]
        public void ClipAt_IsTheLastClipStartingAtOrBefore(double seconds, int expected)
        {
            PositionConverter.ClipAt(T.Clips, seconds).Should().Be(expected);
            PositionConverter.ClipAt(Array.Empty<ReadAlongClip>(), 1).Should().Be(-1);
        }

        [Fact]
        public void ToLocator_IsATextLocatorOnTheSentence()
        {
            var locator = PositionConverter.ToLocator(T, 21, 30)!;
            locator.Href.Should().Be("OEBPS/Text/ch2.xhtml");
            locator.Type.Should().Be("application/xhtml+xml");
            locator.Title.Should().Be("Two");
            locator.Locations!.Fragments.Should().Equal("s2");
            locator.Locations.Progression.Should().BeApproximately(1 / 3d, 1e-9);
            locator.Locations.TotalProgression.Should().BeApproximately(0.7, 1e-9);
            PositionConverter.ToLocator(new Timeline(), 1, 30).Should().BeNull();
        }

        [Fact]
        public void ToSeconds_TextLocatorByFragment()
        {
            var locator = new ReadiumLocator { Href = "OEBPS/Text/ch2.xhtml", Type = "application/xhtml+xml", Locations = new ReadiumLocations { Fragments = new[] { "s3" } } };
            PositionConverter.ToSeconds(T, locator, 30).Should().Be(25);
        }

        [Fact]
        public void ToSeconds_UnknownFragment_FallsBackToChapterProgression()
        {
            var locator = new ReadiumLocator { Href = "/OEBPS/Text/ch1.xhtml", Type = "application/xhtml+xml", Locations = new ReadiumLocations { Fragments = new[] { "gone" }, Progression = 0.5 } };
            PositionConverter.ToSeconds(T, locator, 30).Should().Be(6);
        }

        [Fact]
        public void ToSeconds_AudioLocator_UsesChunkOffset()
        {
            var locator = new ReadiumLocator { Href = "OEBPS/Audio/00001-00002.mp4", Type = "audio/mp4", Locations = new ReadiumLocations { Fragments = new[] { "t=3.5" } } };
            PositionConverter.ToSeconds(T, locator, 30).Should().Be(23.5);
            var relative = new ReadiumLocator { Href = "Audio/00001-00002.mp4", Type = "audio/mp4", Locations = new ReadiumLocations { Fragments = new[] { "t=1" } } };
            PositionConverter.ToSeconds(T, relative, 30).Should().Be(21);
        }

        [Fact]
        public void ToSeconds_UnknownResource_FallsBackToTotalProgression_ThenNull()
        {
            var locator = new ReadiumLocator { Href = "elsewhere.xhtml", Type = "application/xhtml+xml", Locations = new ReadiumLocations { TotalProgression = 0.5 } };
            PositionConverter.ToSeconds(T, locator, 30).Should().Be(15);
            PositionConverter.ToSeconds(T, new ReadiumLocator { Href = "elsewhere.xhtml", Type = "application/xhtml+xml" }, 30).Should().BeNull();
        }

        [Fact]
        public void RoundTrip_SecondsToLocatorAndBack_LandsOnTheSentenceStart()
        {
            var locator = PositionConverter.ToLocator(T, 7.9, 30)!;
            PositionConverter.ToSeconds(T, locator, 30).Should().Be(6);
        }
    }
}
