using System.Collections.Generic;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class ReadAlongServiceTests
    {
        private static readonly StorytellerBook Aligned = new() { Uuid = "u1", Title = "Harbor Winds", AlignedAt = "2026-11-21" };
        private static readonly StorytellerBook Queued = new() { Uuid = "u2", Title = "Queued", AlignedAt = null };

        [Fact]
        public void StatusFor_WalksTheLadder()
        {
            var timeline = TimelineFixture.Build();
            ReadAlongService.StatusFor(false, Aligned, timeline, 30).Should().Be(ReadAlongStatus.Unlinked);
            ReadAlongService.StatusFor(true, null, null, 30).Should().Be(ReadAlongStatus.Unmatched);
            ReadAlongService.StatusFor(true, Queued, null, 30).Should().Be(ReadAlongStatus.Aligning);
            ReadAlongService.StatusFor(true, Aligned, null, 30).Should().Be(ReadAlongStatus.Aligning, "aligned but the timeline could not be fetched yet");
            ReadAlongService.StatusFor(true, Aligned, timeline, 30).Should().Be(ReadAlongStatus.Ready);
            ReadAlongService.StatusFor(true, Aligned, timeline, 300).Should().Be(ReadAlongStatus.Mismatch);
        }

        [Fact]
        public void ToResponse_Ready_CarriesClipsPartsAndWireStatus()
        {
            var parts = new List<BookPart> { new("a", 20), new("b", 10) };
            var anchor = new PartInfo("a", "Harbor Winds", "Harbor Winds", "f", 1, 200_000_000);
            var resolved = new ResolvedBook(new StorytellerLink(), "http://st", anchor, parts, Aligned, TimelineFixture.Build(), ReadAlongStatus.Ready);

            var response = ReadAlongService.ToResponse(resolved);

            response.Status.Should().Be("ready");
            response.BookUuid.Should().Be("u1");
            response.Title.Should().Be("Harbor Winds");
            response.Duration.Should().Be(30);
            response.Parts.Should().HaveCount(2);
            response.Parts[1].ItemId.Should().Be("b");
            response.Chapters.Should().HaveCount(3);
            response.Clips.Should().HaveCount(7);
        }

        [Theory]
        [InlineData(ReadAlongStatus.Unlinked, "unlinked")]
        [InlineData(ReadAlongStatus.Unmatched, "unmatched")]
        [InlineData(ReadAlongStatus.Aligning, "aligning")]
        [InlineData(ReadAlongStatus.Mismatch, "mismatch")]
        [InlineData(ReadAlongStatus.Empty, "empty")]
        public void ToResponse_NotReady_HasNoClips_AndLowercaseStatus(ReadAlongStatus status, string wire)
        {
            var parts = new List<BookPart> { new("a", 20) };
            var anchor = new PartInfo("a", "X", null, "f", null, 0);
            var resolved = new ResolvedBook(new StorytellerLink(), "http://st", anchor, parts, status == ReadAlongStatus.Unmatched ? null : Aligned, TimelineFixture.Build(), status);

            var response = ReadAlongService.ToResponse(resolved);

            response.Status.Should().Be(wire);
            response.Clips.Should().BeEmpty();
            response.Chapters.Should().BeEmpty();
            response.Parts.Should().ContainSingle();
            ReadAlongBook.ToWire(status).Should().Be(wire);
        }
    }
}
