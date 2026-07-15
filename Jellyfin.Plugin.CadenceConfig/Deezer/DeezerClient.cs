using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>
    /// Reads a public Deezer playlist via the unauthenticated api.deezer.com. No credentials are
    /// required for public playlists, so nothing here carries a secret. Follows the API's paging
    /// (100 tracks/page) to return the full track list.
    /// </summary>
    public sealed class DeezerClient
    {
        private const string ApiRoot = "https://api.deezer.com";
        private const int MaxPages = 40; // 40 × 100 = 4000 tracks — a sane ceiling.

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DeezerClient> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeezerClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The Jellyfin-provided HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public DeezerClient(IHttpClientFactory httpClientFactory, ILogger<DeezerClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Fetches a public playlist's title + all its tracks (paging through the API). Returns null
        /// when the id is unparseable, the playlist is private/missing, or the fetch fails.
        /// </summary>
        /// <param name="playlistRef">A Deezer playlist URL or bare id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The playlist title and the full track list, or null.</returns>
        public async Task<DeezerImport?> FetchPlaylistAsync(string? playlistRef, CancellationToken cancellationToken)
        {
            var id = DeezerPlaylistUrl.ParseId(playlistRef);
            if (id == null)
            {
                return null;
            }

            var client = _httpClientFactory.CreateClient("CadenceConfig");
            var first = await GetAsync(client, $"{ApiRoot}/playlist/{id}", cancellationToken).ConfigureAwait(false);
            if (first?.Error != null || first?.Tracks == null)
            {
                _logger.LogWarning("Deezer playlist {Id} unavailable: {Msg}", id, first?.Error?.Message ?? "no tracks");
                return null;
            }

            var tracks = new List<DeezerTrack>(first.Tracks.Data);
            var next = first.Tracks.Next;
            for (var page = 0; page < MaxPages && !string.IsNullOrEmpty(next); page++)
            {
                var more = await GetTracksAsync(client, next, cancellationToken).ConfigureAwait(false);
                if (more == null)
                {
                    break;
                }

                tracks.AddRange(more.Data);
                next = more.Next;
            }

            return new DeezerImport(first.Title ?? $"Deezer playlist {id}", tracks);
        }

        private async Task<DeezerPlaylist?> GetAsync(HttpClient client, string url, CancellationToken ct)
        {
            try
            {
                return await client.GetFromJsonAsync<DeezerPlaylist>(url, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                _logger.LogWarning(ex, "Deezer request to {Url} failed.", url);
                return null;
            }
        }

        private async Task<DeezerTrackList?> GetTracksAsync(HttpClient client, string url, CancellationToken ct)
        {
            try
            {
                return await client.GetFromJsonAsync<DeezerTrackList>(url, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                _logger.LogWarning(ex, "Deezer track page {Url} failed.", url);
                return null;
            }
        }
    }
}
