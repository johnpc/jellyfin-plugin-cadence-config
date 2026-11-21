using System;
using System.Collections.Concurrent;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Decides whether a Jellyfin user-data save becomes a Storyteller position write: progress saves
    /// are rate-limited to one per 30 s per (user, book); a stop/finish always goes through; anything
    /// that isn't a playback position is ignored. Pure apart from the "last pushed" clock it keeps.
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
        /// <param name="now">The current time.</param>
        /// <returns>The decision.</returns>
        public PushDecision Decide(Guid userId, string bookKey, UserDataSaveReason reason, DateTimeOffset now)
        {
            if (reason is not (UserDataSaveReason.PlaybackProgress or UserDataSaveReason.PlaybackFinished or UserDataSaveReason.UpdateUserData))
            {
                return PushDecision.Ignore;
            }

            var key = $"{userId:N}|{bookKey}";
            var force = reason == UserDataSaveReason.PlaybackFinished;
            if (!force && _lastPush.TryGetValue(key, out var last) && now - last < MinInterval)
            {
                return PushDecision.Throttled;
            }

            _lastPush[key] = now;
            return PushDecision.Push;
        }
    }
}
