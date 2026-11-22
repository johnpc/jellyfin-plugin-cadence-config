using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class PartOrderTests
    {
        private static PartInfo Part(string id, string name, string? album, int? index, int seconds, string parent = "folder")
        {
            return new PartInfo(id, name, album, parent, index, seconds * 10_000_000L);
        }

        private static readonly List<PartInfo> Library = new()
        {
            Part("hw2", "Harbor Winds", "Harbor Winds", 2, 226),
            Part("ll1", "The Lighthouse Letters", "The Lighthouse Letters", 1, 100),
            Part("hw3", "Harbor Winds", "Harbor Winds", 3, 249),
            Part("hw1", "Harbor Winds", "Harbor Winds", 1, 203),
            Part("solo", "Caffeine", null, null, 3600),
            Part("ll2", "The Lighthouse Letters", "The Lighthouse Letters", 2, 120),
        };

        [Fact]
        public void PartsOf_GroupsByAlbumInPlayOrder()
        {
            var parts = PartOrder.PartsOf(Library[0], Library);
            parts.Select(p => p.ItemId).Should().Equal("hw1", "hw2", "hw3");
            parts.Select(p => p.Duration).Should().Equal(203, 226, 249);
        }

        [Fact]
        public void PartsOf_LooseFileIsItsOwnBook()
        {
            var parts = PartOrder.PartsOf(Library[4], Library);
            parts.Should().ContainSingle().Which.ItemId.Should().Be("solo");
        }

        [Fact]
        public void PartsOf_AnchorMissingFromCandidates_IsStillIncludedOnce()
        {
            var anchor = Part("x2", "X", "X", 2, 5);
            var parts = PartOrder.PartsOf(anchor, new[] { Part("x1", "X", "X", 1, 4) });
            parts.Select(p => p.ItemId).Should().Equal("x1", "x2");
        }

        [Fact]
        public void PartsOf_TiesOnIndexFallBackToName()
        {
            var a = Part("a", "Chapter B", "Book", null, 1);
            var b = Part("b", "Chapter A", "Book", null, 1);
            PartOrder.PartsOf(a, new[] { a, b }).Select(p => p.ItemId).Should().Equal("b", "a");
        }

        [Fact]
        public void Books_OneEntryPerAlbum_AnchoredOnTheFirstPart()
        {
            var books = PartOrder.Books(Library);
            books.Should().HaveCount(3);
            books.Select(b => b.Anchor.ItemId).Should().BeEquivalentTo(new[] { "hw1", "ll1", "solo" });
            books.First(b => b.Anchor.ItemId == "ll1").Parts.Select(p => p.ItemId).Should().Equal("ll1", "ll2");
        }
    }
}
