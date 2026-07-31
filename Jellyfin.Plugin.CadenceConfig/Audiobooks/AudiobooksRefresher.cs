using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Audiobooks
{
    /// <summary>
    /// Rebuilds a user's audiobook library OFF the request thread and stores it in the cache, so no
    /// user ever waits: the controller serves the (possibly stale) cached copy immediately and calls
    /// this to refresh in the background. Deduped per user — concurrent requests for the same user
    /// while a rebuild is in flight don't stack up N redundant recursive-scan builds.
    /// </summary>
    public sealed class AudiobooksRefresher
    {
        private readonly IAudiobooksService _service;
        private readonly AudiobooksCache _cache;
        private readonly ILogger<AudiobooksRefresher> _logger;
        private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();

        /// <summary>Initializes a new instance of the <see cref="AudiobooksRefresher"/> class.</summary>
        /// <param name="service">Computes a user's library.</param>
        /// <param name="cache">The per-user cache to populate.</param>
        /// <param name="logger">The logger.</param>
        public AudiobooksRefresher(
            IAudiobooksService service,
            AudiobooksCache cache,
            ILogger<AudiobooksRefresher> logger)
        {
            _service = service;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>Rebuild + cache the user's library in the background. No-op (returns the running
        /// work) if a rebuild for this user is already in flight. Injected `now` stamps freshness.</summary>
        /// <param name="user">The user to rebuild for.</param>
        /// <param name="now">A func returning current UTC time (stamped when the build completes).</param>
        /// <returns>A task that completes when the rebuild finishes (or immediately if deduped).</returns>
        public Task RefreshAsync(User user, Func<DateTime> now)
        {
            if (!_inFlight.TryAdd(user.Id, 0))
            {
                return Task.CompletedTask; // a rebuild for this user is already running
            }

            return Task.Run(() =>
            {
                try
                {
                    _cache.Set(user.Id, _service.Build(user), now());
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Audiobooks background refresh failed for a user.");
                }
                finally
                {
                    _inFlight.TryRemove(user.Id, out _);
                }
            });
        }
    }
}
