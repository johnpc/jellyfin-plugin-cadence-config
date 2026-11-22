using System;
using System.Collections.Concurrent;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Decides whether a Jellyfin user-data save becomes a Storyteller position write: progress saves
    /// are rate-limited to one per 30 s per (user, book); a stop/finish always goes through; anything
    /// that isn't a playback position is ignored — including a ZERO position, because Jellyfin's
    /// Sessions/Playing progress path saves 0 for audiobooks (verified live 2026-11-21: the first push
    /// after play was 0 s and the throttle then dropped the real 116 s save). A throttled save is not
    /// lost: the bridge keeps the latest and sends it when <see cref="RetryAfter"/> elapses.
    /// Pure apart from the "last pushed" clock it keeps.
    /// </summary>
    public sealed class PushThrottle
    {
        /// <summary>The minimum gap between two progress pushes for the same (user, book).</summary>
        public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(30);

        private readonly ConcurrentDictionary<string, DateTimeOffset> _lastPush = new();

        /// <summary>Classifies a save and records the push time when it decides to push.</summary>
        /// <param name="userId">The user.</param>
        /// <param name="bookKey">A stable key for the book (e.g. its first part id).</param>
        /// <param name="reason">Why Jellyfin saved.</param>
        /// <param name="positionTicks">The saved position; zero is never pushed.</param>
        /// <param name="now">The current time.</param>
        /// <returns>The decision.</returns>
        public PushDecision Decide(Guid userId, string bookKey, UserDataSaveReason reason, long positionTicks, DateTimeOffset now)
        {
            if (positionTicks <= 0
                || reason is not (UserDataSaveReason.PlaybackProgress or UserDataSaveReason.PlaybackFinished or UserDataSaveReason.UpdateUserData))
            {
                return PushDecision.Ignore;
            }

            var key = Key(userId, bookKey);
            var force = reason == UserDataSaveReason.PlaybackFinished;
            if (!force && _lastPush.TryGetValue(key, out var last) && now - last < MinInterval)
            {
                return PushDecision.Throttled;
            }

            _lastPush[key] = now;
            return PushDecision.Push;
        }

        /// <summary>How long until the next push for this (user, book) is allowed (zero when it is now).</summary>
        /// <param name="userId">The user.</param>
        /// <param name="bookKey">The book key.</param>
        /// <param name="now">The current time.</param>
        /// <returns>The remaining wait.</returns>
        public TimeSpan RetryAfter(Guid userId, string bookKey, DateTimeOffset now)
        {
            if (!_lastPush.TryGetValue(Key(userId, bookKey), out var last))
            {
                return TimeSpan.Zero;
            }

            var remaining = MinInterval - (now - last);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        /// <summary>Records that a deferred (trailing) push went out now, so the window restarts.</summary>
        /// <param name="userId">The user.</param>
        /// <param name="bookKey">The book key.</param>
        /// <param name="now">The current time.</param>
        public void MarkPushed(Guid userId, string bookKey, DateTimeOffset now) => _lastPush[Key(userId, bookKey)] = now;

        private static string Key(Guid userId, string bookKey) => $"{userId:N}|{bookKey}";
    }
}
