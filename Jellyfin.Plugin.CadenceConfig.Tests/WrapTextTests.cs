using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Covers;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class WrapTextTests
    {
        // Deterministic measurer: 10px per character (incl. spaces).
        private static float Measure(string s) => s.Length * 10f;

        [Fact]
        public void KeepsAShortNameOnOneLine()
        {
            var lines = WrapText.Wrap("Yacht Rock", Measure, 400f, 3);
            lines.Should().Equal("Yacht Rock");
        }

        [Fact]
        public void WrapsToMultipleLinesAtTheWidth()
        {
            // width 100 → ~10 chars per line.
            var lines = WrapText.Wrap("one two three four", Measure, 100f, 3);
            lines.Should().Equal("one two", "three four");
        }

        [Fact]
        public void OverflowStaysOnTheLastAllowedLine()
        {
            // maxLines 2, narrow width → the tail packs onto line 2 even past width.
            var lines = WrapText.Wrap("alpha beta gamma delta epsilon", Measure, 60f, 2);
            lines.Should().HaveCount(2);
            lines[1].Should().Contain("gamma").And.Contain("epsilon");
        }

        [Fact]
        public void AlwaysReturnsAtLeastOneLine()
        {
            WrapText.Wrap("Solo", Measure, 400f, 3).Should().ContainSingle();
        }
    }
}
