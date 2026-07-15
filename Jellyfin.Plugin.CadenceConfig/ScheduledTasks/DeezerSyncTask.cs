using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Sync;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.ScheduledTasks
{
    /// <summary>
    /// Keeps every mirrored Jellyfin playlist in step with its source Deezer playlist. On each run it
    /// re-matches each saved subscription against the user's current library and additively adds any
    /// newly-owned tracks — so once Lidarr fills in a previously-missing artist, the next run drops
    /// that track into the playlist. Additive only: it never removes tracks. A thin orchestrator that
    /// delegates the real work to <see cref="DeezerImportService"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class DeezerSyncTask : IScheduledTask
    {
        private readonly DeezerImportService _importService;
        private readonly ILogger<DeezerSyncTask> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeezerSyncTask"/> class.
        /// </summary>
        /// <param name="importService">The shared Deezer import/sync engine.</param>
        /// <param name="logger">The logger.</param>
        public DeezerSyncTask(DeezerImportService importService, ILogger<DeezerSyncTask> logger)
        {
            _importService = importService;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "Sync Deezer Playlists";

        /// <inheritdoc />
        public string Key => "CadenceConfigDeezerSync";

        /// <inheritdoc />
        public string Description =>
            "Adds newly-available library tracks to each Jellyfin playlist imported from Deezer, keeping it in sync as the library grows.";

        /// <inheritdoc />
        public string Category => "Library";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            var hours = Plugin.GetConfiguration().SyncIntervalHours;
            if (hours <= 0)
            {
                hours = 12;
            }

            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.IntervalTrigger,
                    IntervalTicks = TimeSpan.FromHours(hours).Ticks,
                },
            };
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(progress);
            progress.Report(0);

            var subscriptions = Plugin.GetConfiguration().DeezerSubscriptions;
            if (subscriptions.Length == 0)
            {
                _logger.LogInformation("Deezer sync: no subscriptions to sync.");
                progress.Report(100);
                return;
            }

            var totalAdded = 0;
            for (var i = 0; i < subscriptions.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalAdded += await _importService.SyncSubscriptionAsync(subscriptions[i], cancellationToken).ConfigureAwait(false);
                progress.Report((i + 1) * 100.0 / subscriptions.Length);
            }

            _logger.LogInformation(
                "Deezer sync complete: {Count} subscription(s), {Added} track(s) added.",
                subscriptions.Length,
                totalAdded);
        }
    }
}
