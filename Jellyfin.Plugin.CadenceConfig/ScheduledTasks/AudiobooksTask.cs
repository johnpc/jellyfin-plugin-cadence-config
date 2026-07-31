using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Audiobooks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.ScheduledTasks
{
    /// <summary>
    /// DAILY pre-warm of every user's audiobook library into the shared <see cref="AudiobooksCache"/>,
    /// so GET /Cadence/Audiobooks serves it instantly and cold misses are rare. Between daily runs,
    /// an active user's library is kept current by the controller's stale-while-revalidate background
    /// refresh (AudiobooksRefresher) on their own visits — so we DON'T need a tight interval hammering
    /// all N users' recursive scans. Daily is the safety-net warm-up; per-user freshness is
    /// demand-driven. A newly-imported book surfaces within the freshness window on the user's next
    /// visit (or immediately via the refresh action).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class AudiobooksTask : IScheduledTask
    {
        private readonly IAudiobooksService _service;
        private readonly AudiobooksCache _cache;
        private readonly IUserManager _userManager;
        private readonly ILogger<AudiobooksTask> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudiobooksTask"/> class.
        /// </summary>
        /// <param name="service">The library compute service.</param>
        /// <param name="cache">The per-user cache the controller reads.</param>
        /// <param name="userManager">Enumerates the users to precompute for.</param>
        /// <param name="logger">The logger.</param>
        public AudiobooksTask(
            IAudiobooksService service,
            AudiobooksCache cache,
            IUserManager userManager,
            ILogger<AudiobooksTask> logger)
        {
            _service = service;
            _cache = cache;
            _userManager = userManager;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "Precompute Cadence Audiobook Library";

        /// <inheritdoc />
        public string Key => "CadenceConfigAudiobooks";

        /// <inheritdoc />
        public string Description =>
            "Precomputes each user's Cadence audiobook library so the app loads the Audiobooks tab in one fast request instead of a slow recursive library scan.";

        /// <inheritdoc />
        public string Category => "Library";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.IntervalTrigger,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks,
                },
            };
        }

        /// <inheritdoc />
        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(progress);
            progress.Report(0);

            var userIds = _userManager.GetUsersIds().ToList();
            for (var i = 0; i < userIds.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var user = _userManager.GetUserById(userIds[i]);
                if (user is not null)
                {
                    try
                    {
                        _cache.Set(user.Id, _service.Build(user), DateTime.UtcNow);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning(ex, "Audiobooks: failed to precompute for a user.");
                    }
                }

                progress.Report((i + 1) * 100.0 / Math.Max(userIds.Count, 1));
            }

            _logger.LogInformation("Audiobook library precomputed for {Count} user(s).", userIds.Count);
            return Task.CompletedTask;
        }
    }
}
