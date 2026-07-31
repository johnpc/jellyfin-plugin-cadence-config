using System;
using System.Collections.Concurrent;

namespace Jellyfin.Plugin.CadenceConfig.Home
{
    /// <summary>
    /// Per-user cache of computed Home shelves, with a freshness timestamp so the controller can do
    /// stale-while-revalidate: a fresh entry is served instantly; a stale one is STILL served
    /// instantly (no user ever waits) while a background refresh runs; a miss returns null so the
    /// controller triggers a background build and lets the client fall back to native queries for
    /// that one request. The daily <c>HomeShelvesTask</c> pre-warms every user so misses are rare.
    /// Thread-safe: the task/background refreshes write while requests read (ConcurrentDictionary +
    /// atomic swap — Get never blocks on a rebuild).
    /// </summary>
    public sealed class HomeShelvesCache
    {
        /// <summary>How long a cached entry is considered fresh; past this it's served stale while a
        /// background refresh runs. Pre-warmed daily, so this mainly bounds how stale an active
        /// user's shelves get between the daily run and their next visit's background refresh.</summary>
        public static readonly TimeSpan FreshFor = TimeSpan.FromHours(6);

        private readonly ConcurrentDictionary<Guid, (HomeShelvesResult Result, DateTime At)> _byUser
            = new();

        /// <summary>Stores freshly-computed shelves for a user, stamped now.</summary>
        /// <param name="userId">The user id.</param>
        /// <param name="result">The computed shelves.</param>
        /// <param name="now">The current UTC time (injected for testability).</param>
        public void Set(Guid userId, HomeShelvesResult result, DateTime now) =>
            _byUser[userId] = (result, now);

        /// <summary>Gets the cached entry for a user, or null on a miss. `stale` is true when the
        /// entry is older than <see cref="FreshFor"/> — the caller serves it anyway and refreshes
        /// in the background.</summary>
        /// <param name="userId">The user id.</param>
        /// <param name="now">The current UTC time (injected for testability).</param>
        /// <returns>The cached shelves + whether they're stale, or null on a miss.</returns>
        public (HomeShelvesResult Result, bool Stale)? Get(Guid userId, DateTime now)
        {
            if (!_byUser.TryGetValue(userId, out var entry))
            {
                return null;
            }

            return (entry.Result, now - entry.At > FreshFor);
        }

        /// <summary>Drops a user's cached entry so the next request is a cold miss (rebuilt fresh).
        /// Used by the "refresh Home" action so a user can force-regenerate their shelves.</summary>
        /// <param name="userId">The user id to invalidate.</param>
        public void Invalidate(Guid userId) => _byUser.TryRemove(userId, out _);

        /// <summary>Drops EVERY user's cached entry (admin "regenerate all"). The next request per
        /// user is a cold miss that rebuilds fresh (or the daily task repopulates).</summary>
        public void Clear() => _byUser.Clear();
    }
}
