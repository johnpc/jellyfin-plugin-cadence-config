using System;
using System.Collections.Concurrent;

namespace Jellyfin.Plugin.CadenceConfig.Home
{
    /// <summary>
    /// Holds the last-computed Home shelves per user, keyed by user id. The scheduled
    /// <c>HomeShelvesTask</c> recomputes and stores; the controller reads instantly. A miss (never
    /// computed, or task hasn't run yet) returns null so the controller computes on demand for that
    /// one request — the client still gets an answer, just not from cache. Thread-safe: the task
    /// writes while requests read.
    /// </summary>
    public sealed class HomeShelvesCache
    {
        private readonly ConcurrentDictionary<Guid, HomeShelvesResult> _byUser = new();

        /// <summary>Stores the freshly-computed shelves for a user.</summary>
        /// <param name="userId">The user id.</param>
        /// <param name="result">The computed shelves.</param>
        public void Set(Guid userId, HomeShelvesResult result) => _byUser[userId] = result;

        /// <summary>Gets the cached shelves for a user, or null on a miss.</summary>
        /// <param name="userId">The user id.</param>
        /// <returns>The cached shelves, or null.</returns>
        public HomeShelvesResult? Get(Guid userId) =>
            _byUser.TryGetValue(userId, out var result) ? result : null;
    }
}
