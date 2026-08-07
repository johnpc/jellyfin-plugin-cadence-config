using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Audiobooks;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.CadenceConfig.ScheduledTasks
{
    /// <summary>
    /// Warms the audiobook-library cache so the first client request after a server start or a nightly
    /// scan is already fast (never pays the full scan on the request path). Event-based invalidation
    /// (<see cref="AudiobookLibraryInvalidator"/>) keeps the cache correct between runs; this task just
    /// pre-builds it. Runs on startup and daily. A thin orchestrator over
    /// <see cref="AudiobookLibraryService.Rebuild"/>, excluded from coverage.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class AudiobookLibraryRefreshTask : IScheduledTask
    {
        private readonly AudiobookLibraryService _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudiobookLibraryRefreshTask"/> class.
        /// </summary>
        /// <param name="service">The audiobook-library cache to warm.</param>
        public AudiobookLibraryRefreshTask(AudiobookLibraryService service)
        {
            _service = service;
        }

        /// <inheritdoc />
        public string Name => "Refresh Cadence Audiobook Library";

        /// <inheritdoc />
        public string Key => "CadenceConfigAudiobookLibraryRefresh";

        /// <inheritdoc />
        public string Description =>
            "Pre-builds the cached audiobook library the Cadence client fetches, so opening the Audiobooks tab is fast without a per-request library scan.";

        /// <inheritdoc />
        public string Category => "Library";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger },
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
            _service.Rebuild();
            progress.Report(100);
            return Task.CompletedTask;
        }
    }
}
