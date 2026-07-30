using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Covers;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.ScheduledTasks
{
    /// <summary>
    /// Generates a mosaic Primary image for every playlist that has none, so all clients show a real
    /// cover instead of a placeholder — Jellyfin doesn't auto-generate playlist art. Never overwrites
    /// an existing cover. A thin orchestrator over <see cref="PlaylistCoverService"/>; also runs once
    /// on library-scan-adjacent schedules so newly-created playlists get a cover without waiting.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class PlaylistCoverTask : IScheduledTask
    {
        private readonly PlaylistCoverService _service;
        private readonly ILogger<PlaylistCoverTask> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistCoverTask"/> class.
        /// </summary>
        /// <param name="service">The playlist-cover generation service.</param>
        /// <param name="logger">The logger.</param>
        public PlaylistCoverTask(PlaylistCoverService service, ILogger<PlaylistCoverTask> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "Generate Playlist Covers";

        /// <inheritdoc />
        public string Key => "CadenceConfigPlaylistCovers";

        /// <inheritdoc />
        public string Description =>
            "Creates a mosaic cover image for playlists that don't have one, from their tracks' album art.";

        /// <inheritdoc />
        public string Category => "Library";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(6).Ticks,
            },
        };

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(progress);
            progress.Report(0);
            var count = await _service.GenerateAsync(progress, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Playlist cover task complete: {Count} generated.", count);
            progress.Report(100);
        }
    }
}
