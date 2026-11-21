using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// The PUSH half of Whispersync, as a hosted service: every time Jellyfin saves a user's playback
    /// position on an audiobook (any client — Cadence, the web UI, Finamp…), the position is converted
    /// to a Storyteller text locator and written there, so the Storyteller reader opens at the sentence
    /// the listener stopped on. Throttled to one write per 30 s per book (stops always go through) and
    /// blind to the plugin's own pull writes.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class PositionBridge : IHostedService
    {
        private readonly IUserDataManager _userDataManager;
        private readonly ReadAlongService _readAlong;
        private readonly PositionService _positions;
        private readonly ILogger<PositionBridge> _logger;
        private readonly PushThrottle _throttle = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="PositionBridge"/> class.
        /// </summary>
        /// <param name="userDataManager">Jellyfin's user-data manager (the event source).</param>
        /// <param name="readAlong">Resolves books.</param>
        /// <param name="positions">Pushes positions.</param>
        /// <param name="logger">The logger.</param>
        public PositionBridge(IUserDataManager userDataManager, ReadAlongService readAlong, PositionService positions, ILogger<PositionBridge> logger)
        {
            _userDataManager = userDataManager;
            _readAlong = readAlong;
            _positions = positions;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _userDataManager.UserDataSaved += OnUserDataSaved;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _userDataManager.UserDataSaved -= OnUserDataSaved;
            return Task.CompletedTask;
        }

        private void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
        {
            // Only audiobooks: music tracks are Audio too, and resolving every song save against the
            // Storyteller catalog would be pointless work on every play.
            if (e.Item is not AudioBook audio || e.UserData == null || string.IsNullOrWhiteSpace(Plugin.GetConfiguration().StorytellerUrl))
            {
                return;
            }

            var itemId = audio.Id.ToString("N");
            if (_positions.IsOurWrite(e.UserId, itemId))
            {
                return;
            }

            var bookKey = string.IsNullOrWhiteSpace(audio.Album) ? itemId : audio.Album;
            if (_throttle.Decide(e.UserId, bookKey, e.SaveReason, DateTimeOffset.UtcNow) != PushDecision.Push)
            {
                return;
            }

            var seconds = PartsTimeline.TicksToSeconds(e.UserData.PlaybackPositionTicks);
            _ = PushAsync(e.UserId, audio.Id, itemId, seconds);
        }

        private async Task PushAsync(Guid userId, Guid itemGuid, string itemId, double partSeconds)
        {
            try
            {
                var resolved = await _readAlong.ResolveAsync(userId, itemGuid, CancellationToken.None).ConfigureAwait(false);
                if (resolved == null || resolved.Status != ReadAlongStatus.Ready)
                {
                    return;
                }

                var global = PartsTimeline.ToGlobal(resolved.Parts, itemId, partSeconds);
                if (global == null)
                {
                    return;
                }

                var outcome = await _positions.PushAsync(resolved, global.Value, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), CancellationToken.None).ConfigureAwait(false);
                _logger.LogDebug("CadenceConfig: pushed {Seconds:F0}s of {Book} to Storyteller: {Outcome}.", global, resolved.Book?.Title, outcome);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger.LogWarning(ex, "CadenceConfig: Storyteller push failed.");
            }
        }
    }
}
