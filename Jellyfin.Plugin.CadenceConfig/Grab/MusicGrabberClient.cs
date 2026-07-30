using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Grab
{
    /// <summary>
    /// Server-side client for the self-hosted Music Grabber (musicgrabber.jpc.io): search a query,
    /// start a single-track download, and poll the job to completion. Config (URL + key) comes from
    /// the plugin configuration so the key never has to ship in a client bundle. Thin HTTP plumbing —
    /// the result-choosing logic is the pure, tested <see cref="GrabPick"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class MusicGrabberClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MusicGrabberClient> _logger;

        /// <summary>Initializes a new instance of the <see cref="MusicGrabberClient"/> class.</summary>
        /// <param name="httpClientFactory">The Jellyfin HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public MusicGrabberClient(IHttpClientFactory httpClientFactory, ILogger<MusicGrabberClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>Whether a base URL + API key are configured (else the grabber path is skipped).</summary>
        /// <returns>True when the grabber is usable.</returns>
        public bool IsConfigured()
        {
            var cfg = Plugin.GetConfiguration();
            return !string.IsNullOrWhiteSpace(cfg.MusicGrabberUrl) && !string.IsNullOrWhiteSpace(cfg.MusicGrabberApiKey);
        }

        /// <summary>
        /// Grab a single Deezer track: search, pick the best result (<see cref="GrabPick"/>), then
        /// download it — passing the CLEAN Deezer metadata (title/artist/album/track#) so the grabber
        /// tags + files it correctly in the shared music library (a raw YouTube grab is
        /// "youtube_guessed" with no album). The grabber writes the file and rescans itself, so we
        /// just poll the job to completion. Returns true on success. Never throws — logs and returns false.
        /// </summary>
        /// <param name="track">The Deezer track to fetch (title/artist/album/position).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True when the track was downloaded.</returns>
        public async Task<bool> GrabAsync(DeezerTrack track, CancellationToken cancellationToken)
        {
            var title = track.Title ?? string.Empty;
            var artist = track.Artist?.Name;
            try
            {
                var cfg = Plugin.GetConfiguration();
                var baseUrl = cfg.MusicGrabberUrl.TrimEnd('/');
                var client = _httpClientFactory.CreateClient("CadenceConfig");

                var query = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}";

                // Search source "youtube" (fast single source), NOT "all": the multi-source search is
                // slow and returns 0 under the concurrent load of a playlist import (verified live).
                var search = await PostAsync<GrabSearchResponse>(
                    client,
                    baseUrl + "/api/search",
                    cfg.MusicGrabberApiKey,
                    new { query, limit = 15, source = "youtube" },
                    cancellationToken).ConfigureAwait(false);
                var pick = search is null ? null : GrabPick.Best(search.Results, title, artist);
                if (search is null)
                {
                    _logger.LogWarning("Music Grabber: search returned null for {Artist} - {Title}.", artist, title);
                    return false;
                }

                if (pick is null)
                {
                    _logger.LogWarning(
                        "Music Grabber: no matching result among {Count} for {Artist} - {Title}.",
                        search.Results.Count,
                        artist,
                        title);
                    return false;
                }

                var job = await PostAsync<GrabJob>(
                    client,
                    baseUrl + "/api/download",
                    cfg.MusicGrabberApiKey,
                    new
                    {
                        video_id = pick.VideoId,
                        title = pick.Title,
                        artist = pick.Artist ?? pick.Channel ?? artist ?? string.Empty,
                        source = pick.Source,
                        source_url = pick.SourceUrl,
                        search_token = search.SearchToken,
                        download_type = "single",
                        convert_to_flac = true,
                        slskd_username = pick.SlskdUsername,
                        slskd_filename = pick.SlskdFilename,
                        slskd_size = pick.SlskdSize,

                        // Clean Deezer metadata → the grabber tags + organises the file itself.
                        album_artist = artist,
                        album_name = track.Album?.Title,
                        album_track_title = title,
                        album_track_number = track.TrackPosition > 0 ? track.TrackPosition : (int?)null,
                    },
                    cancellationToken).ConfigureAwait(false);

                if (job?.Id is null)
                {
                    _logger.LogWarning("Music Grabber: download did not start for {Artist} - {Title}.", artist, title);
                    return false;
                }

                return await PollAsync(client, baseUrl, cfg.MusicGrabberApiKey, job.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Music Grabber: grab failed for {Artist} - {Title}.", artist, title);
                return false;
            }
        }

        private async Task<bool> PollAsync(HttpClient client, string baseUrl, string key, string jobId, CancellationToken cancellationToken)
        {
            for (var i = 0; i < 60; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/jobs/{Uri.EscapeDataString(jobId)}");
                req.Headers.Add("X-API-Key", key);
                using var res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode)
                {
                    continue;
                }

                var job = await res.Content.ReadFromJsonAsync<GrabJob>(cancellationToken).ConfigureAwait(false);
                if (string.Equals(job?.Status, "completed", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(job?.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return false; // timed out (5 min) — treat as not grabbed
        }

#pragma warning disable CA1822 // instance (not static) to satisfy SA1204 ordering after instance members
        private async Task<T?> PostAsync<T>(HttpClient client, string url, string key, object body, CancellationToken cancellationToken)
            where T : class
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
            req.Headers.Add("X-API-Key", key);
            using var res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Music Grabber: POST {Url} → HTTP {Code}.", url, (int)res.StatusCode);
                return null;
            }

            return await res.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }
#pragma warning restore CA1822
    }
}
