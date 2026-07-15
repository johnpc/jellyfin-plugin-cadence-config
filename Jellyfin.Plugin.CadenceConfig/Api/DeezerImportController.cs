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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// Imports a public Deezer playlist into a Jellyfin playlist for the calling user: reads the
    /// Deezer playlist (no auth needed), matches its tracks against the user's audio library, creates
    /// a playlist of the tracks found, and returns the artists whose tracks are missing so the client
    /// can offer to request them via Lidarr. The match itself is the pure, unit-tested DeezerMatcher.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence/Deezer")]
    [ExcludeFromCodeCoverage]
    public class DeezerImportController : ControllerBase
    {
        private readonly DeezerClient _deezer;
        private readonly ILibraryManager _libraryManager;
        private readonly IPlaylistManager _playlistManager;
        private readonly ILogger<DeezerImportController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeezerImportController"/> class.
        /// </summary>
        /// <param name="deezer">The Deezer API client.</param>
        /// <param name="libraryManager">Jellyfin library manager (to index audio items).</param>
        /// <param name="playlistManager">Jellyfin playlist manager (to create + fill the playlist).</param>
        /// <param name="logger">The logger.</param>
        public DeezerImportController(
            DeezerClient deezer,
            ILibraryManager libraryManager,
            IPlaylistManager playlistManager,
            ILogger<DeezerImportController> logger)
        {
            _deezer = deezer;
            _libraryManager = libraryManager;
            _playlistManager = playlistManager;
            _logger = logger;
        }

        /// <summary>
        /// Imports a public Deezer playlist for the given user.
        /// </summary>
        /// <param name="userId">The calling user's Jellyfin id (playlist owner + library scope).</param>
        /// <param name="url">A Deezer playlist share URL or bare id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The created playlist + missing artists, or an error status.</returns>
        [HttpPost("Import")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<DeezerImportResult>> Import(
            [FromQuery] Guid userId,
            [FromQuery] string? url,
            CancellationToken cancellationToken)
        {
            var imported = await _deezer.FetchPlaylistAsync(url, cancellationToken).ConfigureAwait(false);
            if (imported == null)
            {
                return BadRequest("Could not read that Deezer playlist (private, not found, or bad URL).");
            }

            var index = BuildLibraryIndex(userId);
            var match = DeezerMatcher.Match(imported.Tracks, index);

            var playlist = await _playlistManager.CreatePlaylist(new PlaylistCreationRequest
            {
                Name = imported.Title,
                ItemIdList = match.FoundItemIds.Select(Guid.Parse).ToArray(),
                UserId = userId,
                MediaType = MediaType.Audio,
            }).ConfigureAwait(false);

            _logger.LogInformation(
                "Deezer import '{Name}': {Added}/{Total} tracks matched, {Missing} artists missing.",
                imported.Title,
                match.FoundCount,
                imported.Tracks.Count,
                match.MissingArtistCount);

            return new DeezerImportResult
            {
                PlaylistId = playlist.Id,
                PlaylistName = imported.Title,
                AddedCount = match.FoundCount,
                TotalCount = imported.Tracks.Count,
                MissingArtists = match.MissingArtists,
            };
        }

        /// <summary>
        /// Builds a normalized (artist, title) → item id index of the user's audio library, so the
        /// matcher can resolve Deezer tracks to owned Jellyfin items. First key wins on collisions.
        /// </summary>
        /// <param name="userId">The user whose library to index.</param>
        /// <returns>The TrackKey → item id map.</returns>
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
