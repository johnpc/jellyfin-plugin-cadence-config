using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.CadenceConfig.Covers;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using Jellyfin.Plugin.CadenceConfig.Grab;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Playlists;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Sync
{
    /// <summary>
    /// The shared engine behind the one-shot Deezer import and the scheduled re-sync: read a public
    /// Deezer playlist, match against the user's library, create/top-up the Jellyfin playlist, cover
    /// it, and grab the missing tracks. Set math is the pure <see cref="PlaylistSync"/>/
    /// <see cref="DeezerMatcher"/>; this is the live-Jellyfin plumbing, so it's excluded from coverage.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class DeezerImportService
    {
        private readonly DeezerClient _deezer;
        private readonly ILibraryManager _libraryManager;
        private readonly IPlaylistManager _playlistManager;
        private readonly GrabFulfillmentService _grabFulfillment;
        private readonly PlaylistCoverService _coverService;
        private readonly ILogger<DeezerImportService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeezerImportService"/> class.
        /// </summary>
        /// <param name="deezer">The Deezer API client.</param>
        /// <param name="libraryManager">Jellyfin library manager (to index audio items).</param>
        /// <param name="playlistManager">Jellyfin playlist manager (to create/read/fill playlists).</param>
        /// <param name="grabFulfillment">Fetches missing tracks via Music Grabber + tags them.</param>
        /// <param name="coverService">Generates the imported playlist's cover.</param>
        /// <param name="logger">The logger.</param>
        public DeezerImportService(
            DeezerClient deezer,
            ILibraryManager libraryManager,
            IPlaylistManager playlistManager,
            GrabFulfillmentService grabFulfillment,
            PlaylistCoverService coverService,
            ILogger<DeezerImportService> logger)
        {
            _deezer = deezer;
            _libraryManager = libraryManager;
            _playlistManager = playlistManager;
            _grabFulfillment = grabFulfillment;
            _coverService = coverService;
            _logger = logger;
        }

        /// <summary>
        /// Imports a Deezer playlist for a user: matches, creates or reuses the Jellyfin playlist,
        /// additively adds newly-owned tracks, and records a subscription so the task keeps it fresh.
        /// </summary>
        /// <param name="userId">The owning user id.</param>
        /// <param name="url">A Deezer playlist URL or bare id.</param>
        /// <param name="isPublic">When true, mark the playlist public so it appears in the shared/community view.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The import result, or null when the Deezer playlist could not be read.</returns>
        public async Task<DeezerImportResult?> ImportAsync(Guid userId, string? url, bool isPublic, CancellationToken cancellationToken)
        {
            var imported = await _deezer.FetchPlaylistAsync(url, cancellationToken).ConfigureAwait(false);
            if (imported == null)
            {
                return null;
            }

            var deezerId = DeezerPlaylistUrl.ParseId(url) ?? string.Empty;
            var match = DeezerMatcher.Match(imported.Tracks, BuildLibraryIndex(userId));

            var existing = DeezerSubscriptionStore.Find(userId, deezerId);
            var playlistId = await ResolvePlaylistAsync(existing, imported.Title, userId).ConfigureAwait(false);
            var added = await AddNewTracksAsync(playlistId, userId, match.FoundItemIds).ConfigureAwait(false);
            if (isPublic)
            {
                await MakePublicAsync(playlistId, cancellationToken).ConfigureAwait(false);
            }

            DeezerSubscriptionStore.Save(userId, deezerId, playlistId, match.MissingArtists);
            await CoverAndGrabAsync(playlistId, match.MissingTracks, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Deezer import '{Name}': {Matched}/{Total} matched, {Added} newly added, {Missing} artists missing, {MissingTracks} tracks queued to grab.",
                imported.Title,
                match.FoundCount,
                imported.Tracks.Count,
                added,
                match.MissingArtistCount,
                match.MissingTracks.Count);

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
            DeezerSubscriptionStore.Save(subscription.UserId, subscription.DeezerPlaylistId, subscription.JellyfinPlaylistId, match.MissingArtists);

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
            DeezerSubscriptionStore.Save(userId, sub.DeezerPlaylistId, sub.JellyfinPlaylistId, match.MissingArtists);
            return new DeezerSubscriptionStatus(sub.DeezerPlaylistId, match.MissingArtists);
        }

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
            await _playlistManager.AddItemToPlaylistAsync(playlistGuid, itemGuids, position: null, userId).ConfigureAwait(false);
            return additions.Count;
        }

        private Dictionary<TrackKey, string> BuildLibraryIndex(Guid userId) =>
            LibraryIndex.Build(_libraryManager, userId);

        /// <summary>Mark the playlist public (OpenAccess) so it surfaces in the shared/community view.
        /// Set on the entity directly because Jellyfin's UpdatePlaylist API resolves the user from the
        /// request token, which a server-side/API-key call lacks. No-op when the id isn't a playlist.</summary>
        private async Task MakePublicAsync(string playlistId, CancellationToken cancellationToken)
        {
            if (Guid.TryParse(playlistId, out var pid)
                && _libraryManager.GetItemById(pid) is Playlist playlist
                && !playlist.OpenAccess)
            {
                playlist.OpenAccess = true;
                await playlist.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Deezer import: marked '{Name}' public.", playlist.Name);
            }
        }

        /// <summary>Post-import side-effects: cover the new playlist now (not on the 6h task), and
        /// kick off Music Grabber for the missing tracks in the background (the sync task folds the
        /// landed files in later). No-ops where uncovered/ungrabbable or the grabber's unset.</summary>
        private async Task CoverAndGrabAsync(string playlistId, IReadOnlyList<DeezerTrack> missing, CancellationToken cancellationToken)
        {
            if (Guid.TryParse(playlistId, out var pid))
            {
                await _coverService.CoverPlaylistAsync(pid, cancellationToken).ConfigureAwait(false);
            }

            _ = Task.Run(() => _grabFulfillment.FulfillAsync(missing, CancellationToken.None));
        }
    }
}
