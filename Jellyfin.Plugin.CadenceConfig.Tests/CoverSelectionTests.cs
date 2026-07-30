using System.Collections.Generic;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Covers;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class CoverSelectionTests
    {
        [Fact]
        public void PicksUpToFourDistinctPaths_InOrder()
        {
            var result = CoverSelection.PickTiles(new List<string?> { "a", "b", "c", "d", "e" });
            result.Should().Equal("a", "b", "c", "d");
        }

        [Fact]
        public void DedupesRepeatedPaths_SoAMosaicIsntFourCopies()
        {
            // A playlist that's all one album → one distinct tile, not four.
            var result = CoverSelection.PickTiles(new List<string?> { "same", "same", "same" });
            result.Should().Equal("same");
        }

        [Fact]
        public void SkipsNullAndEmptyPaths()
        {
            var result = CoverSelection.PickTiles(new List<string?> { null, "", "a", null, "b" });
            result.Should().Equal("a", "b");
        }

        [Fact]
        public void ReturnsEmpty_WhenNoUsableArt()
        {
            CoverSelection.PickTiles(new List<string?> { null, "", null }).Should().BeEmpty();
        }

        [Fact]
        public void HonoursACustomMax()
        {
            CoverSelection.PickTiles(new List<string?> { "a", "b", "c" }, max: 1).Should().Equal("a");
        }
    }
}
