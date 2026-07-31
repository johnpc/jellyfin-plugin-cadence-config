using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Home;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.ScheduledTasks
{
    /// <summary>
    /// DAILY pre-warm of every user's Home shelves into the shared <see cref="HomeShelvesCache"/>,
    /// so GET /Cadence/Home serves them instantly and cold misses are rare. Between daily runs, an
    /// active user's shelves are kept current by the controller's stale-while-revalidate background
    /// refresh (HomeShelvesRefresher) on their own visits — so we DON'T need a tight interval
    /// hammering all N users' recursive scans every few minutes. Daily is the safety-net warm-up;
    /// per-user freshness is demand-driven. A brand-new like/follow surfaces within the freshness
    /// window on the user's next visit (the client also reflects likes optimistically meanwhile).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class HomeShelvesTask : IScheduledTask
    {
        private readonly IHomeShelvesService _service;
        private readonly HomeShelvesCache _cache;
        private readonly IUserManager _userManager;
        private readonly ILogger<HomeShelvesTask> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeShelvesTask"/> class.
        /// </summary>
        /// <param name="service">The shelves compute service.</param>
        /// <param name="cache">The per-user cache the controller reads.</param>
        /// <param name="userManager">Enumerates the users to precompute for.</param>
        /// <param name="logger">The logger.</param>
        public HomeShelvesTask(
            IHomeShelvesService service,
            HomeShelvesCache cache,
            IUserManager userManager,
            ILogger<HomeShelvesTask> logger)
        {
            _service = service;
            _cache = cache;
            _userManager = userManager;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "Precompute Cadence Home Shelves";

        /// <inheritdoc />
        public string Key => "CadenceConfigHomeShelves";

        /// <inheritdoc />
        public string Description =>
            "Precomputes each user's Cadence Home shelves so the app loads Home in one fast request instead of several slow library scans.";

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
                        _logger.LogWarning(ex, "Home shelves: failed to precompute for a user.");
                    }
                }

                progress.Report((i + 1) * 100.0 / Math.Max(userIds.Count, 1));
            }

            _logger.LogInformation("Home shelves precomputed for {Count} user(s).", userIds.Count);
            return Task.CompletedTask;
        }
    }
}
