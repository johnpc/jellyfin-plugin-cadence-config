using System.Collections.Generic;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class PartsTimelineTests
    {
        private static readonly List<BookPart> Parts = new()
        {
            new BookPart("a", 203),
            new BookPart("b", 226),
            new BookPart("c", 249),
        };

        [Fact]
        public void Total_SumsParts() => PartsTimeline.Total(Parts).Should().Be(678);

        [Fact]
        public void ToGlobal_AddsEarlierParts()
        {
            PartsTimeline.ToGlobal(Parts, "a", 10).Should().Be(10);
            PartsTimeline.ToGlobal(Parts, "B", 10).Should().Be(213);
            PartsTimeline.ToGlobal(Parts, "c", -5).Should().Be(429);
            PartsTimeline.ToGlobal(Parts, "zzz", 1).Should().BeNull();
        }

        [Fact]
        public void Locate_FindsPartAndOffset_ClampingAtEnds()
        {
            PartsTimeline.Locate(Parts, 0)!.Value.Should().Be((Parts[0], 0d));
            PartsTimeline.Locate(Parts, 203)!.Value.Should().Be((Parts[1], 0d));
            PartsTimeline.Locate(Parts, 500)!.Value.Should().Be((Parts[2], 71d));
            PartsTimeline.Locate(Parts, 99_999)!.Value.Should().Be((Parts[2], 249d));
            PartsTimeline.Locate(Parts, -3)!.Value.Should().Be((Parts[0], 0d));
            PartsTimeline.Locate(new List<BookPart>(), 1).Should().BeNull();
        }

        [Fact]
        public void Ticks_RoundTrip()
        {
            PartsTimeline.TicksToSeconds(15_000_000).Should().Be(1.5);
            PartsTimeline.TicksToSeconds(null).Should().Be(0);
            PartsTimeline.SecondsToTicks(1.5).Should().Be(15_000_000);
            PartsTimeline.SecondsToTicks(-2).Should().Be(0);
        }
    }
}
