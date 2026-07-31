using System;
using System.Collections.Concurrent;

namespace Jellyfin.Plugin.CadenceConfig.Audiobooks
{
    /// <summary>
    /// Per-user cache of the computed audiobook library, with a freshness timestamp so the controller
    /// can do stale-while-revalidate: a fresh entry is served instantly; a stale one is STILL served
    /// instantly (no user ever waits) while a background refresh runs; a miss returns null so the
    /// controller triggers a background build and lets the client fall back to the native scan for
    /// that one request. The daily <c>AudiobooksTask</c> pre-warms every user so misses are rare. The
    /// audiobook library changes rarely (new books are imported infrequently), so a long freshness
    /// window is fine. Thread-safe: the task/background refreshes write while requests read
    /// (ConcurrentDictionary + atomic swap — Get never blocks on a rebuild).
    /// </summary>
    public sealed class AudiobooksCache
    {
        /// <summary>How long a cached entry is considered fresh; past this it's served stale while a
        /// background refresh runs. Longer than Home's window — the audiobook library is far more
        /// static than a user's play history, so re-scanning it often would be wasted work.</summary>
        public static readonly TimeSpan FreshFor = TimeSpan.FromHours(24);

        private readonly ConcurrentDictionary<Guid, (AudiobooksResult Result, DateTime At)> _byUser
            = new();

        /// <summary>Stores a freshly-computed library for a user, stamped now.</summary>
        /// <param name="userId">The user id.</param>
        /// <param name="result">The computed library.</param>
        /// <param name="now">The current UTC time (injected for testability).</param>
        public void Set(Guid userId, AudiobooksResult result, DateTime now) =>
            _byUser[userId] = (result, now);

        /// <summary>Gets the cached entry for a user, or null on a miss. `stale` is true when the
        /// entry is older than <see cref="FreshFor"/> — the caller serves it anyway and refreshes
        /// in the background.</summary>
        /// <param name="userId">The user id.</param>
        /// <param name="now">The current UTC time (injected for testability).</param>
        /// <returns>The cached library + whether it's stale, or null on a miss.</returns>
        public (AudiobooksResult Result, bool Stale)? Get(Guid userId, DateTime now)
        {
            if (!_byUser.TryGetValue(userId, out var entry))
            {
                return null;
            }

            return (entry.Result, now - entry.At > FreshFor);
        }

        /// <summary>Drops a user's cached entry so the next request is a cold miss (rebuilt fresh).
        /// Used by the "refresh" action so a user can force-regenerate their library after importing
        /// new books.</summary>
        /// <param name="userId">The user id to invalidate.</param>
        public void Invalidate(Guid userId) => _byUser.TryRemove(userId, out _);

        /// <summary>Drops EVERY user's cached entry (admin "regenerate all"). The next request per
        /// user is a cold miss that rebuilds fresh (or the daily task repopulates).</summary>
        public void Clear() => _byUser.Clear();
    }
}
