using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.ScheduledTasks
{
    /// <summary>
    /// The PULL half of Whispersync: for every linked user and every audiobook that Storyteller has
    /// aligned, read Storyteller's position and, when it is newer than Jellyfin's, write it into the
    /// saved playback position of the right part — so a book read on the couch resumes at that spot in
    /// any Jellyfin client. Thin orchestrator; the rule is <see cref="SyncDecision"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class StorytellerSyncTask : IScheduledTask
    {
        private readonly ReadAlongService _readAlong;
        private readonly PositionService _positions;
        private readonly JellyfinBooks _jellyfin;
        private readonly StorytellerClient _client;
        private readonly ILogger<StorytellerSyncTask> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorytellerSyncTask"/> class.
        /// </summary>
        /// <param name="readAlong">Resolves books.</param>
        /// <param name="positions">Reads/writes positions.</param>
        /// <param name="jellyfin">Jellyfin plumbing.</param>
        /// <param name="client">The Storyteller client.</param>
        /// <param name="logger">The logger.</param>
        public StorytellerSyncTask(ReadAlongService readAlong, PositionService positions, JellyfinBooks jellyfin, StorytellerClient client, ILogger<StorytellerSyncTask> logger)
        {
            _readAlong = readAlong;
            _positions = positions;
            _jellyfin = jellyfin;
            _client = client;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "Sync Storyteller positions";

        /// <inheritdoc />
        public string Key => "CadenceConfigStorytellerSync";

        /// <inheritdoc />
        public string Description => "Pulls each linked user's Storyteller reading position into the matching audiobook's saved playback position (Whispersync).";

        /// <inheritdoc />
        public string Category => "Library";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            var minutes = Plugin.GetConfiguration().StorytellerSyncIntervalMinutes;
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.IntervalTrigger,
                    IntervalTicks = TimeSpan.FromMinutes(minutes <= 0 ? 5 : minutes).Ticks,
                },
            };
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(progress);
            progress.Report(0);
            var config = Plugin.GetConfiguration();
            var baseUrl = StorytellerUrls.Base(config.StorytellerUrl);
            var links = config.StorytellerLinks ?? Array.Empty<StorytellerLink>();
            if (baseUrl == null || links.Length == 0)
            {
                progress.Report(100);
                return;
            }

            var books = PartOrder.Books(_jellyfin.AllParts());
            var pulled = 0;
            var done = 0;
            foreach (var link in links.Where(l => !string.IsNullOrEmpty(l.Token)))
            {
                foreach (var (anchor, parts) in books)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pulled += await SyncOneAsync(link, baseUrl, anchor, parts, cancellationToken).ConfigureAwait(false) ? 1 : 0;
                }

                progress.Report(100.0 * ++done / links.Length);
            }

            _logger.LogInformation("CadenceConfig: Storyteller sync pulled {Count} position(s) for {Users} user(s).", pulled, links.Length);
            progress.Report(100);
        }

        private async Task<bool> SyncOneAsync(StorytellerLink link, string baseUrl, PartInfo anchor, IReadOnlyList<BookPart> parts, CancellationToken cancellationToken)
        {
            var resolved = await _readAlong.ResolveLinkedAsync(link, baseUrl, anchor, parts, cancellationToken).ConfigureAwait(false);
            if (resolved.Status != ReadAlongStatus.Ready || resolved.Book == null)
            {
                return false;
            }

            var theirs = await _client.GetPositionAsync(baseUrl, link.Token, resolved.Book.Uuid, cancellationToken).ConfigureAwait(false);
            var ours = _positions.JellyfinPosition(resolved);
            var lastPlayed = ours == null || ours.Timestamp == 0 ? (DateTime?)null : DateTimeOffset.FromUnixTimeMilliseconds(ours.Timestamp).UtcDateTime;
            if (SyncDecision.Decide(theirs?.Timestamp, lastPlayed) != SyncOutcome.PullToJellyfin)
            {
                return false;
            }

            var seconds = PositionConverter.ToSeconds(resolved.Timeline!, theirs!.Locator, PartsTimeline.Total(parts));
            return seconds != null && _positions.Pull(resolved, seconds.Value, theirs.Timestamp, cancellationToken);
        }
    }
}
