using System;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class SyncDecisionTests
    {
        private static readonly DateTime Noon = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
        private static long Ms(DateTime t) => new DateTimeOffset(t).ToUnixTimeMilliseconds();

        [Fact]
        public void NoStorytellerPosition_NothingToPull()
        {
            SyncDecision.Decide(null, Noon).Should().Be(SyncOutcome.NothingToPull);
        }

        [Fact]
        public void NoJellyfinPosition_Pulls()
        {
            SyncDecision.Decide(Ms(Noon), null).Should().Be(SyncOutcome.PullToJellyfin);
        }

        [Fact]
        public void NewerStoryteller_Pulls_WithinSlackDoesNot()
        {
            SyncDecision.Decide(Ms(Noon.AddMinutes(1)), Noon).Should().Be(SyncOutcome.PullToJellyfin);
            SyncDecision.Decide(Ms(Noon.AddSeconds(1)), Noon).Should().Be(SyncOutcome.JellyfinCurrent);
            SyncDecision.Decide(Ms(Noon), Noon).Should().Be(SyncOutcome.JellyfinCurrent);
            SyncDecision.Decide(Ms(Noon.AddMinutes(-5)), Noon).Should().Be(SyncOutcome.JellyfinCurrent);
        }

        [Fact]
        public void UnspecifiedKind_IsTreatedAsUtc()
        {
            var unspecified = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Unspecified);
            SyncDecision.Decide(Ms(Noon.AddMinutes(1)), unspecified).Should().Be(SyncOutcome.PullToJellyfin);
        }
    }
}
