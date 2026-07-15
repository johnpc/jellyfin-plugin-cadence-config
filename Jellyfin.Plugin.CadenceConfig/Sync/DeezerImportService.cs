using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Playlists;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Sync
{
    /// <summary>
    /// The shared engine behind both the one-shot Deezer import (the API controller) and the
    /// scheduled re-sync (the task): read a public Deezer playlist, match its tracks against a
    /// user's library, then create the Jellyfin playlist (first import) or additively top up the
    /// existing one (re-sync). The set math is the pure, unit-tested <see cref="PlaylistSync"/> and
    /// <see cref="DeezerMatcher"/>; this class is the live-Jellyfin plumbing around them, so it is
    /// excluded from coverage exactly like the controllers.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class DeezerImportService
    {
        private readonly DeezerClient _deezer;
        private readonly ILibraryManager _libraryManager;
        private readonly IPlaylistManager _playlistManager;
        private readonly ILogger<DeezerImportService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeezerImportService"/> class.
        /// </summary>
        /// <param name="deezer">The Deezer API client.</param>
        /// <param name="libraryManager">Jellyfin library manager (to index audio items).</param>
        /// <param name="playlistManager">Jellyfin playlist manager (to create/read/fill playlists).</param>
        /// <param name="logger">The logger.</param>
        public DeezerImportService(
            DeezerClient deezer,
            ILibraryManager libraryManager,
            IPlaylistManager playlistManager,
            ILogger<DeezerImportService> logger)
        {
            _deezer = deezer;
            _libraryManager = libraryManager;
            _playlistManager = playlistManager;
            _logger = logger;
        }

        /// <summary>
        /// Imports a Deezer playlist for a user: matches, creates or reuses the Jellyfin playlist,
        /// additively adds newly-owned tracks, and records a subscription so the task keeps it fresh.
        /// </summary>
        /// <param name="userId">The owning user id.</param>
        /// <param name="url">A Deezer playlist URL or bare id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The import result, or null when the Deezer playlist could not be read.</returns>
        public async Task<DeezerImportResult?> ImportAsync(Guid userId, string? url, CancellationToken cancellationToken)
        {
            var imported = await _deezer.FetchPlaylistAsync(url, cancellationToken).ConfigureAwait(false);
            if (imported == null)
            {
                return null;
            }

            var deezerId = DeezerPlaylistUrl.ParseId(url) ?? string.Empty;
            var match = DeezerMatcher.Match(imported.Tracks, BuildLibraryIndex(userId));

            var existing = FindSubscription(userId, deezerId);
            var playlistId = await ResolvePlaylistAsync(existing, imported.Title, userId).ConfigureAwait(false);
            var added = await AddNewTracksAsync(playlistId, userId, match.FoundItemIds).ConfigureAwait(false);

            SaveSubscription(userId, deezerId, playlistId, match.MissingArtists);

            _logger.LogInformation(
                "Deezer import '{Name}': {Matched}/{Total} matched, {Added} newly added, {Missing} artists missing.",
                imported.Title,
                match.FoundCount,
                imported.Tracks.Count,
                added,
                match.MissingArtistCount);

            return new DeezerImportResult
            {
                PlaylistId = playlistId,
                PlaylistName = imported.Title,
                AddedCount = match.FoundCount,
                TotalCount = imported.Tracks.Count,
                MissingArtists = match.MissingArtists,
            };
        }

        /// <summary>
        /// Re-syncs one existing subscription: re-matches against the (possibly grown) library and
        /// additively adds any newly-owned tracks to the already-created Jellyfin playlist.
        /// </summary>
        /// <param name="subscription">The subscription to refresh.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of tracks newly added to the playlist.</returns>
        public async Task<int> SyncSubscriptionAsync(DeezerSubscription subscription, CancellationToken cancellationToken)
        {
            var imported = await _deezer.FetchPlaylistAsync(subscription.DeezerPlaylistId, cancellationToken).ConfigureAwait(false);
            if (imported == null)
            {
                _logger.LogWarning("Deezer subscription {Id} unavailable this run; skipping.", subscription.DeezerPlaylistId);
                return 0;
            }

            var match = DeezerMatcher.Match(imported.Tracks, BuildLibraryIndex(subscription.UserId));
            var added = await AddNewTracksAsync(subscription.JellyfinPlaylistId, subscription.UserId, match.FoundItemIds).ConfigureAwait(false);

            // Refresh the stored missing-artist list so the client's playlist page reflects the
            // shrinking gap as Lidarr fills artists in, even between reads.
            SaveSubscription(subscription.UserId, subscription.DeezerPlaylistId, subscription.JellyfinPlaylistId, match.MissingArtists);

            _logger.LogInformation(
                "Deezer sync '{Name}': {Added} newly added ({Matched}/{Total} now owned, {Missing} artists missing).",
                imported.Title,
                added,
                match.FoundCount,
                imported.Tracks.Count,
                match.MissingArtistCount);

            return added;
        }

        /// <summary>
        /// The current missing artists for a mirrored playlist, RECOMPUTED against the user's library
        /// so an artist Lidarr has since filled in drops off immediately. Re-reads the Deezer playlist
        /// and re-matches; if Deezer is unreachable this run, falls back to the persisted list so the
        /// page still shows something. Returns null when no subscription mirrors that Jellyfin playlist.
        /// </summary>
        /// <param name="userId">The calling user id (must own the subscription).</param>
        /// <param name="jellyfinPlaylistId">The Jellyfin playlist id shown on the client.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The Deezer playlist id + current missing artists, or null when not a subscription.</returns>
        public async Task<DeezerSubscriptionStatus?> GetMissingArtistsAsync(Guid userId, string jellyfinPlaylistId, CancellationToken cancellationToken)
        {
            var sub = Array.Find(
                Plugin.GetConfiguration().DeezerSubscriptions,
                s => s.UserId == userId && string.Equals(s.JellyfinPlaylistId, jellyfinPlaylistId, StringComparison.Ordinal));
            if (sub == null)
            {
                return null;
            }

            var imported = await _deezer.FetchPlaylistAsync(sub.DeezerPlaylistId, cancellationToken).ConfigureAwait(false);
            if (imported == null)
            {
                // Deezer unreachable — return the last-known persisted list rather than nothing.
                return new DeezerSubscriptionStatus(sub.DeezerPlaylistId, sub.MissingArtists);
            }

            var match = DeezerMatcher.Match(imported.Tracks, BuildLibraryIndex(userId));
            SaveSubscription(userId, sub.DeezerPlaylistId, sub.JellyfinPlaylistId, match.MissingArtists);
            return new DeezerSubscriptionStatus(sub.DeezerPlaylistId, match.MissingArtists);
        }

        private static DeezerSubscription? FindSubscription(Guid userId, string deezerId) =>
            Array.Find(
                Plugin.GetConfiguration().DeezerSubscriptions,
                s => s.UserId == userId && string.Equals(s.DeezerPlaylistId, deezerId, StringComparison.Ordinal));

        private async Task<string> ResolvePlaylistAsync(DeezerSubscription? existing, string title, Guid userId)
        {
            if (existing != null
                && Guid.TryParse(existing.JellyfinPlaylistId, out var id)
                && _playlistManager.GetPlaylistForUser(id, userId) != null)
            {
                return existing.JellyfinPlaylistId;
            }

            var created = await _playlistManager.CreatePlaylist(new PlaylistCreationRequest
            {
                Name = title,
                UserId = userId,
                MediaType = MediaType.Audio,
            }).ConfigureAwait(false);
            return created.Id;
        }

        private async Task<int> AddNewTracksAsync(string playlistId, Guid userId, IReadOnlyList<string> candidateIds)
        {
            if (!Guid.TryParse(playlistId, out var playlistGuid))
            {
                return 0;
            }

            var playlist = _playlistManager.GetPlaylistForUser(playlistGuid, userId);
            var existingIds = playlist == null
                ? Enumerable.Empty<string>()
                : playlist.GetManageableItems().Select(entry => entry.Item2.Id.ToString("N"));

            var additions = PlaylistSync.ComputeAdditions(existingIds, candidateIds);
            if (additions.Count == 0)
            {
                return 0;
            }

            var itemGuids = additions.Select(id => Guid.ParseExact(id, "N")).ToArray();
            await _playlistManager.AddItemToPlaylistAsync(playlistGuid, itemGuids, userId).ConfigureAwait(false);
            return additions.Count;
        }

        private void SaveSubscription(Guid userId, string deezerId, string playlistId, IReadOnlyList<string> missingArtists)
        {
            if (string.IsNullOrEmpty(deezerId))
            {
                return;
            }

            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                return;
            }

            var kept = plugin.Configuration.DeezerSubscriptions
                .Where(s => !(s.UserId == userId && string.Equals(s.DeezerPlaylistId, deezerId, StringComparison.Ordinal)))
                .Append(new DeezerSubscription
                {
                    UserId = userId,
                    DeezerPlaylistId = deezerId,
                    JellyfinPlaylistId = playlistId,
                    MissingArtists = missingArtists.ToArray(),
                })
                .ToArray();

            plugin.Configuration.DeezerSubscriptions = kept;
            plugin.SaveConfiguration();
        }

        private Dictionary<TrackKey, string> BuildLibraryIndex(Guid userId)
        {
            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Audio },
                Recursive = true,
            });

            var index = new Dictionary<TrackKey, string>();
            foreach (var item in items.OfType<Audio>())
            {
                var artist = item.Artists.Count > 0
                    ? item.Artists[0]
                    : item.AlbumArtists.Count > 0 ? item.AlbumArtists[0] : null;
                var key = new TrackKey(artist, item.Name);
                if (key.IsValid && !index.ContainsKey(key))
                {
                    index[key] = item.Id.ToString("N");
                }
            }

            return index;
        }
    }
}
