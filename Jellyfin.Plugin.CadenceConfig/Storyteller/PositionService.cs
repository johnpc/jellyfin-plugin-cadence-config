using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// The two directions of the Whispersync bridge for one resolved book: read the newer of the two
    /// sides' positions, push a Jellyfin position to Storyteller, pull Storyteller's into Jellyfin.
    /// The merge rule is the pure <see cref="Merge"/>; the writes mark themselves in flight so the
    /// user-data event they raise is recognised as our own and not pushed back (no ping-pong).
    /// </summary>
    public sealed class PositionService
    {
        private readonly StorytellerClient _client;
        private readonly JellyfinBooks _jellyfin;
        private readonly ILogger<PositionService> _logger;
        private readonly ConcurrentDictionary<string, byte> _writing = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="PositionService"/> class.
        /// </summary>
        /// <param name="client">The Storyteller client.</param>
        /// <param name="jellyfin">Jellyfin plumbing.</param>
        /// <param name="logger">The logger.</param>
        public PositionService(StorytellerClient client, JellyfinBooks jellyfin, ILogger<PositionService> logger)
        {
            _client = client;
            _jellyfin = jellyfin;
            _logger = logger;
        }

        /// <summary>Picks the newer of the two sides (ties → Jellyfin, the local truth).</summary>
        /// <param name="storyteller">Storyteller's position in global seconds, or null.</param>
        /// <param name="jellyfin">Jellyfin's position in global seconds, or null.</param>
        /// <returns>The winner, or null when neither has one.</returns>
        public static MergedPosition? Merge(MergedPosition? storyteller, MergedPosition? jellyfin)
        {
            if (storyteller == null)
            {
                return jellyfin;
            }

            if (jellyfin == null)
            {
                return storyteller;
            }

            return storyteller.Timestamp > jellyfin.Timestamp ? storyteller : jellyfin;
        }

        /// <summary>Whether a plugin write to this (user, part) is in flight — i.e. the event is ours.</summary>
        /// <param name="userId">The user.</param>
        /// <param name="itemId">The part.</param>
        /// <returns>True while writing.</returns>
        public bool IsOurWrite(Guid userId, string itemId) => _writing.ContainsKey(Key(userId, itemId));

        /// <summary>Jellyfin's saved position for the book as a global position (its most recently played part).</summary>
        /// <param name="resolved">The resolved book.</param>
        /// <returns>The position, or null.</returns>
        [ExcludeFromCodeCoverage]
        public MergedPosition? JellyfinPosition(ResolvedBook resolved)
        {
            var user = _jellyfin.UserOf(resolved.Link.JellyfinUserId);
            if (user == null)
            {
                return null;
            }

            var saved = _jellyfin.SavedPositions(user, resolved.Parts)
                .Where(p => p.Seconds > 0 || p.LastPlayed != null)
                .OrderByDescending(p => p.LastPlayed ?? DateTime.MinValue)
                .FirstOrDefault();
            if (saved.ItemId == null)
            {
                return null;
            }

            var global = PartsTimeline.ToGlobal(resolved.Parts, saved.ItemId, saved.Seconds);
            var stamp = saved.LastPlayed.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(saved.LastPlayed.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
                : 0;
            return global == null ? null : new MergedPosition(global.Value, stamp, "jellyfin");
        }

        /// <summary>Storyteller's position converted to global seconds, or null.</summary>
        /// <param name="resolved">The resolved book (must have a timeline).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The position, or null.</returns>
        [ExcludeFromCodeCoverage]
        public async Task<MergedPosition?> StorytellerPositionAsync(ResolvedBook resolved, CancellationToken cancellationToken)
        {
            if (resolved.Book == null || resolved.Timeline == null)
            {
                return null;
            }

            var position = await _client.GetPositionAsync(resolved.BaseUrl, resolved.Link.Token, resolved.Book.Uuid, cancellationToken).ConfigureAwait(false);
            if (position == null)
            {
                return null;
            }

            var seconds = PositionConverter.ToSeconds(resolved.Timeline, position.Locator, PartsTimeline.Total(resolved.Parts));
            return seconds == null ? null : new MergedPosition(seconds.Value, position.Timestamp, "storyteller");
        }

        /// <summary>Pushes a global position to Storyteller as a text locator.</summary>
        /// <param name="resolved">The resolved book (must be ready).</param>
        /// <param name="seconds">Global seconds.</param>
        /// <param name="timestamp">Wall-clock stamp, ms.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The outcome.</returns>
        [ExcludeFromCodeCoverage]
        public Task<PutPositionOutcome> PushAsync(ResolvedBook resolved, double seconds, long timestamp, CancellationToken cancellationToken)
        {
            if (resolved.Book == null || resolved.Timeline == null)
            {
                return Task.FromResult(PutPositionOutcome.Failed);
            }

            var locator = PositionConverter.ToLocator(resolved.Timeline, seconds, PartsTimeline.Total(resolved.Parts));
            if (locator == null)
            {
                return Task.FromResult(PutPositionOutcome.Failed);
            }

            return _client.PutPositionAsync(resolved.BaseUrl, resolved.Link.Token, resolved.Book.Uuid, new StorytellerPosition { Locator = locator, Timestamp = timestamp }, cancellationToken);
        }

        /// <summary>Writes a global position into Jellyfin's saved position for the right part.</summary>
        /// <param name="resolved">The resolved book.</param>
        /// <param name="seconds">Global seconds.</param>
        /// <param name="timestamp">The stamp to record as LastPlayedDate (ms).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True when written.</returns>
        [ExcludeFromCodeCoverage]
        public bool Pull(ResolvedBook resolved, double seconds, long timestamp, CancellationToken cancellationToken)
        {
            var user = _jellyfin.UserOf(resolved.Link.JellyfinUserId);
            var target = PartsTimeline.Locate(resolved.Parts, seconds);
            if (user == null || target == null)
            {
                return false;
            }

            var keys = resolved.Parts.Select(p => Key(user.Id, p.ItemId)).ToList();
            keys.ForEach(k => _writing[k] = 1);
            try
            {
                _jellyfin.WritePosition(user, resolved.Parts, target.Value.Part.ItemId, target.Value.Offset, DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime, cancellationToken);
                _logger.LogInformation("CadenceConfig: pulled Storyteller position {Seconds:F0}s into {Book} for {User}.", seconds, resolved.Anchor.Album ?? resolved.Anchor.Name, user.Username);
                return true;
            }
            finally
            {
                keys.ForEach(k => _writing.TryRemove(k, out _));
            }
        }

        private static string Key(Guid userId, string itemId) => $"{userId:N}|{itemId}".ToUpperInvariant();
    }
}
