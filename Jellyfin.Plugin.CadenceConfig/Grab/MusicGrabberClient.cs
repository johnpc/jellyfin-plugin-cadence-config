using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
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
        /// Grab a single track for "artist - title": search, pick the best result (<see cref="GrabPick"/>),
        /// download it, and poll until done. Returns the downloaded file path, or null if nothing
        /// suitable was found or the job failed. Never throws — logs and returns null.
        /// </summary>
        /// <param name="title">The wanted track title.</param>
        /// <param name="artist">The wanted artist name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The downloaded file path, or null.</returns>
        public async Task<string?> GrabAsync(string title, string? artist, CancellationToken cancellationToken)
        {
            try
            {
                var cfg = Plugin.GetConfiguration();
                var baseUrl = cfg.MusicGrabberUrl.TrimEnd('/');
                var client = _httpClientFactory.CreateClient("CadenceConfig");

                var query = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}";
                var search = await PostAsync<GrabSearchResponse>(
                    client,
                    baseUrl + "/api/search",
                    cfg.MusicGrabberApiKey,
                    new { query, limit = 15, source = "all" },
                    cancellationToken).ConfigureAwait(false);
                var pick = search is null ? null : GrabPick.Best(search.Results, title, artist);
                if (pick is null || search is null)
                {
                    return null;
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
                    },
                    cancellationToken).ConfigureAwait(false);

                return job?.JobId is null ? null : await PollAsync(client, baseUrl, cfg.MusicGrabberApiKey, job.JobId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Music Grabber: grab failed for {Artist} - {Title}.", artist, title);
                return null;
            }
        }

        private async Task<string?> PollAsync(HttpClient client, string baseUrl, string key, string jobId, CancellationToken cancellationToken)
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
                    return job?.FilePath;
                }

                if (string.Equals(job?.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            return null; // timed out (5 min) — treat as not grabbed
        }

#pragma warning disable CA1822 // instance (not static) to satisfy SA1204 ordering after instance members
        private async Task<T?> PostAsync<T>(HttpClient client, string url, string key, object body, CancellationToken cancellationToken)
            where T : class
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
            req.Headers.Add("X-API-Key", key);
            using var res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            return res.IsSuccessStatusCode
                ? await res.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false)
                : null;
        }
#pragma warning restore CA1822
    }
}
