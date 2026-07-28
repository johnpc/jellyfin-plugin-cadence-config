using System.Collections.Generic;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Chapters;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class ChapterMapperTests
    {
        private const long Sec = 10_000_000;

        [Fact]
        public void NullOrEmpty_ReturnsEmpty()
        {
            ChapterMapper.ToClientChapters(null).Should().BeEmpty();
            ChapterMapper.ToClientChapters(new List<ChapterInfo>()).Should().BeEmpty();
        }

        [Fact]
        public void ConvertsTicksToSecondsAndKeepsNames()
        {
            var markers = new List<ChapterInfo>
            {
                new() { Name = "Intro", StartPositionTicks = 0 },
                new() { Name = "Chapter One", StartPositionTicks = 90 * Sec },
            };

            var result = ChapterMapper.ToClientChapters(markers);

            result.Should().HaveCount(2);
            result[0].Name.Should().Be("Intro");
            result[0].Start.Should().Be(0);
            result[1].Name.Should().Be("Chapter One");
            result[1].Start.Should().Be(90);
        }

        [Fact]
        public void OrdersByStartAndNamesUnnamedMarkers()
        {
            var markers = new List<ChapterInfo>
            {
                new() { Name = null, StartPositionTicks = 120 * Sec },
                new() { Name = "  ", StartPositionTicks = 10 * Sec },
            };

            var result = ChapterMapper.ToClientChapters(markers);

            result[0].Start.Should().Be(10);
            result[0].Name.Should().Be("Chapter 1");
            result[1].Start.Should().Be(120);
            result[1].Name.Should().Be("Chapter 2");
        }

        [Fact]
        public void SingleZeroMarker_IsTreatedAsNoChapters()
        {
            var markers = new List<ChapterInfo> { new() { Name = "Chapter 1", StartPositionTicks = 0 } };
            ChapterMapper.ToClientChapters(markers).Should().BeEmpty();
        }

        [Fact]
        public void AllMarkersAtZero_IsTreatedAsNoChapters()
        {
            var markers = new List<ChapterInfo>
            {
                new() { Name = "A", StartPositionTicks = 0 },
                new() { Name = "B", StartPositionTicks = 0 },
            };
            ChapterMapper.ToClientChapters(markers).Should().BeEmpty();
        }

        [Fact]
        public void SingleRealChapter_IsKept()
        {
            var markers = new List<ChapterInfo> { new() { Name = "Only", StartPositionTicks = 5 * Sec } };
            var result = ChapterMapper.ToClientChapters(markers);
            result.Should().ContainSingle();
            result[0].Start.Should().Be(5);
        }
    }
}
