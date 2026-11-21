using System;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class PushThrottleTests
    {
        private static readonly Guid User = Guid.NewGuid();
        private static readonly DateTimeOffset T0 = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Progress_PushesThenThrottlesUntilTheIntervalPasses()
        {
            var throttle = new PushThrottle();
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, T0).Should().Be(PushDecision.Push);
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, T0.AddSeconds(10)).Should().Be(PushDecision.Throttled);
            throttle.Decide(User, "book", UserDataSaveReason.UpdateUserData, T0.AddSeconds(29)).Should().Be(PushDecision.Throttled);
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, T0.AddSeconds(30)).Should().Be(PushDecision.Push);
        }

        [Fact]
        public void Finished_AlwaysPushes_AndResetsTheClock()
        {
            var throttle = new PushThrottle();
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, T0).Should().Be(PushDecision.Push);
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackFinished, T0.AddSeconds(1)).Should().Be(PushDecision.Push);
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, T0.AddSeconds(2)).Should().Be(PushDecision.Throttled);
        }

        [Fact]
        public void OtherBooksAndUsers_AreIndependent()
        {
            var throttle = new PushThrottle();
            throttle.Decide(User, "a", UserDataSaveReason.PlaybackProgress, T0).Should().Be(PushDecision.Push);
            throttle.Decide(User, "b", UserDataSaveReason.PlaybackProgress, T0).Should().Be(PushDecision.Push);
            throttle.Decide(Guid.NewGuid(), "a", UserDataSaveReason.PlaybackProgress, T0).Should().Be(PushDecision.Push);
        }

        [Theory]
        [InlineData(UserDataSaveReason.TogglePlayed)]
        [InlineData(UserDataSaveReason.UpdateUserRating)]
        [InlineData(UserDataSaveReason.Import)]
        [InlineData(UserDataSaveReason.PlaybackStart)]
        public void NonPositionSaves_AreIgnored(UserDataSaveReason reason)
        {
            new PushThrottle().Decide(User, "a", reason, T0).Should().Be(PushDecision.Ignore);
        }
    }
}
