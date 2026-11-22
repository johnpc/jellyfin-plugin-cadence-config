using System;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class PushThrottleTests
    {
        private const long Ticks = 1_162_327_088; // 116 s

        [Fact]
        public void Decide_IgnoresAZeroPosition_EvenWhenNothingWasPushedYet()
        {
            // Jellyfin's Sessions/Playing progress saves 0 for audiobooks; pushing it would move the
            // Storyteller bookmark to the first sentence and then throttle the real position.
            var throttle = new PushThrottle();
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, 0, T0).Should().Be(PushDecision.Ignore);
            throttle.Decide(User, "book", UserDataSaveReason.UpdateUserData, Ticks, T0.AddSeconds(1)).Should().Be(PushDecision.Push);
        }

        [Fact]
        public void RetryAfter_CountsDownTheWindow_AndMarkPushedRestartsIt()
        {
            var throttle = new PushThrottle();
            throttle.RetryAfter(User, "book", T0).Should().Be(System.TimeSpan.Zero);
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, Ticks, T0);
            throttle.RetryAfter(User, "book", T0.AddSeconds(10)).Should().Be(System.TimeSpan.FromSeconds(20));
            throttle.RetryAfter(User, "book", T0.AddSeconds(40)).Should().Be(System.TimeSpan.Zero);
            throttle.MarkPushed(User, "book", T0.AddSeconds(40));
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, Ticks, T0.AddSeconds(50)).Should().Be(PushDecision.Throttled);
        }

        private static readonly Guid User = Guid.NewGuid();
        private static readonly DateTimeOffset T0 = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Progress_PushesThenThrottlesUntilTheIntervalPasses()
        {
            var throttle = new PushThrottle();
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, Ticks, T0).Should().Be(PushDecision.Push);
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, Ticks, T0.AddSeconds(10)).Should().Be(PushDecision.Throttled);
            throttle.Decide(User, "book", UserDataSaveReason.UpdateUserData, Ticks, T0.AddSeconds(29)).Should().Be(PushDecision.Throttled);
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, Ticks, T0.AddSeconds(30)).Should().Be(PushDecision.Push);
        }

        [Fact]
        public void Finished_AlwaysPushes_AndResetsTheClock()
        {
            var throttle = new PushThrottle();
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, Ticks, T0).Should().Be(PushDecision.Push);
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackFinished, Ticks, T0.AddSeconds(1)).Should().Be(PushDecision.Push);
            throttle.Decide(User, "book", UserDataSaveReason.PlaybackProgress, Ticks, T0.AddSeconds(2)).Should().Be(PushDecision.Throttled);
        }

        [Fact]
        public void OtherBooksAndUsers_AreIndependent()
        {
            var throttle = new PushThrottle();
            throttle.Decide(User, "a", UserDataSaveReason.PlaybackProgress, Ticks, T0).Should().Be(PushDecision.Push);
            throttle.Decide(User, "b", UserDataSaveReason.PlaybackProgress, Ticks, T0).Should().Be(PushDecision.Push);
            throttle.Decide(Guid.NewGuid(), "a", UserDataSaveReason.PlaybackProgress, Ticks, T0).Should().Be(PushDecision.Push);
        }

        [Theory]
        [InlineData(UserDataSaveReason.TogglePlayed)]
        [InlineData(UserDataSaveReason.UpdateUserRating)]
        [InlineData(UserDataSaveReason.Import)]
        [InlineData(UserDataSaveReason.PlaybackStart)]
        public void NonPositionSaves_AreIgnored(UserDataSaveReason reason)
        {
            new PushThrottle().Decide(User, "a", reason, Ticks, T0).Should().Be(PushDecision.Ignore);
        }
    }
}
