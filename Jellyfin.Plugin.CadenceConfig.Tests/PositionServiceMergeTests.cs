using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class PositionServiceMergeTests
    {
        [Fact]
        public void Merge_PrefersTheNewerStamp_TiesGoToJellyfin()
        {
            var st = new MergedPosition(10, 2000, "storyteller");
            var jf = new MergedPosition(20, 1000, "jellyfin");
            PositionService.Merge(st, jf).Should().Be(st);
            PositionService.Merge(st with { Timestamp = 1000 }, jf).Should().Be(jf);
            PositionService.Merge(st with { Timestamp = 500 }, jf).Should().Be(jf);
        }

        [Fact]
        public void Merge_HandlesMissingSides()
        {
            var st = new MergedPosition(10, 2000, "storyteller");
            PositionService.Merge(st, null).Should().Be(st);
            PositionService.Merge(null, st).Should().Be(st);
            PositionService.Merge(null, null).Should().BeNull();
        }
    }
}
