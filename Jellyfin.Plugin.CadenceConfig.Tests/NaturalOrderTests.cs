using System.Linq;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class NaturalOrderTests
    {
        [Fact]
        public void SortsEmbeddedNumbersNumerically()
        {
            var files = new[] { "a/00001-00010.mp4", "a/00001-00002.mp4", "a/00002-00001.mp4", "a/00001-00001.mp4" };
            files.OrderBy(f => f, NaturalOrder.Instance).Should().Equal(
                "a/00001-00001.mp4", "a/00001-00002.mp4", "a/00001-00010.mp4", "a/00002-00001.mp4");
        }

        [Fact]
        public void HandlesPrefixesLeadingZerosAndNulls()
        {
            var c = NaturalOrder.Instance;
            c.Compare("part2", "part10").Should().BeNegative();
            c.Compare("part010", "part10").Should().Be(0);
            c.Compare("b", "a").Should().BePositive();
            c.Compare("a", "ab").Should().BeNegative();
            c.Compare(null, string.Empty).Should().Be(0);
        }
    }
}
