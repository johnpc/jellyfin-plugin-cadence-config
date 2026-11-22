using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// The Storyteller v2 API, over Jellyfin's named HTTP client. Every call takes the caller's own
    /// token: Storyteller positions are per Storyteller user, so the plugin always acts as the linked
    /// user, never as an admin. Nothing here is cached; callers decide what to keep.
    /// </summary>
    public sealed class StorytellerClient
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<StorytellerClient> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorytellerClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The Jellyfin-provided HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public StorytellerClient(IHttpClientFactory httpClientFactory, ILogger<StorytellerClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>Exchanges a username/password for a bearer token.</summary>
        /// <param name="baseUrl">The normalized Storyteller base URL.</param>
        /// <param name="username">The Storyteller username or email.</param>
        /// <param name="password">The password (used once, never stored).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The outcome.</returns>
        public async Task<LoginResult> LoginAsync(string baseUrl, string username, string password, CancellationToken cancellationToken)
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["usernameOrEmail"] = username,
                ["password"] = password,
            });
            try
            {
                using var response = await Client().PostAsync(new Uri(StorytellerUrls.Token(baseUrl)), form, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return new LoginResult(LoginOutcome.BadCredentials, null);
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new LoginResult(LoginOutcome.Unreachable, null);
                }

                var body = await response.Content.ReadFromJsonAsync<StorytellerToken>(Json, cancellationToken).ConfigureAwait(false);
                return string.IsNullOrEmpty(body?.AccessToken)
                    ? new LoginResult(LoginOutcome.Unreachable, null)
                    : new LoginResult(LoginOutcome.Ok, body.AccessToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "CadenceConfig: Storyteller login at {Base} failed.", baseUrl);
                return new LoginResult(LoginOutcome.Unreachable, null);
            }
        }

        /// <summary>Lists every book the token's user can see.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's bearer token.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The books, or null when the call failed.</returns>
        public Task<IReadOnlyList<StorytellerBook>?> ListBooksAsync(string baseUrl, string token, CancellationToken cancellationToken)
        {
            return GetJsonAsync<IReadOnlyList<StorytellerBook>>(StorytellerUrls.Books(baseUrl), token, cancellationToken);
        }

        /// <summary>Fetches the readaloud publication's manifest.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's bearer token.</param>
        /// <param name="bookUuid">The book.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The manifest, or null.</returns>
        public Task<ReadiumManifest?> GetManifestAsync(string baseUrl, string token, string bookUuid, CancellationToken cancellationToken)
        {
            return GetJsonAsync<ReadiumManifest>(StorytellerUrls.Read(baseUrl, bookUuid, "manifest.json"), token, cancellationToken);
        }

        /// <summary>Fetches a chapter's guided-navigation (text ↔ audio clip) document.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's bearer token.</param>
        /// <param name="bookUuid">The book.</param>
        /// <param name="chapterHref">The chapter href.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The document, or null.</returns>
        public Task<GuidedNavigationDocument?> GetGuidedNavigationAsync(string baseUrl, string token, string bookUuid, string chapterHref, CancellationToken cancellationToken)
        {
            return GetJsonAsync<GuidedNavigationDocument>(StorytellerUrls.GuidedNavigation(baseUrl, bookUuid, chapterHref), token, cancellationToken);
        }

        /// <summary>
        /// Opens a publication resource as a streaming response (headers read, body not buffered),
        /// optionally with a Range header. The caller disposes the response.
        /// </summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's bearer token.</param>
        /// <param name="bookUuid">The book.</param>
        /// <param name="path">The publication-relative path.</param>
        /// <param name="range">An optional Range header value to forward.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The upstream response.</returns>
        public async Task<HttpResponseMessage> GetResourceAsync(string baseUrl, string token, string bookUuid, string path, RangeHeaderValue? range, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(StorytellerUrls.Read(baseUrl, bookUuid, path)));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Range = range;
            return await Client().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Reads a byte range of a publication resource (for MP4 header probing).</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's bearer token.</param>
        /// <param name="bookUuid">The book.</param>
        /// <param name="path">The publication-relative path.</param>
        /// <param name="offset">First byte.</param>
        /// <param name="length">How many bytes (fewer come back at EOF).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The bytes (possibly empty).</returns>
        public async Task<byte[]> ReadRangeAsync(string baseUrl, string token, string bookUuid, string path, long offset, int length, CancellationToken cancellationToken)
        {
            using var response = await GetResourceAsync(baseUrl, token, bookUuid, path, new RangeHeaderValue(offset, offset + length - 1), cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                return Array.Empty<byte>();
            }

            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            // A server that ignored the Range header returns the whole file: slice it ourselves.
            if (response.StatusCode == HttpStatusCode.OK && bytes.Length > length)
            {
                var start = (int)Math.Min(offset, bytes.Length);
                return bytes[start..Math.Min(bytes.Length, start + length)];
            }

            return bytes;
        }

        /// <summary>Reads the user's stored position for a book.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's bearer token.</param>
        /// <param name="bookUuid">The book.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The position, or null when none is stored (404) or the call failed.</returns>
        public Task<StorytellerPosition?> GetPositionAsync(string baseUrl, string token, string bookUuid, CancellationToken cancellationToken)
        {
            return GetJsonAsync<StorytellerPosition>(StorytellerUrls.Positions(baseUrl, bookUuid), token, cancellationToken);
        }

        /// <summary>Writes the user's position for a book.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's bearer token.</param>
        /// <param name="bookUuid">The book.</param>
        /// <param name="position">The position.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The outcome.</returns>
        public async Task<PutPositionOutcome> PutPositionAsync(string baseUrl, string token, string bookUuid, StorytellerPosition position, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(StorytellerUrls.Positions(baseUrl, bookUuid)));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(position, options: Json);
            try
            {
                using var response = await Client().SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    return PutPositionOutcome.Conflict;
                }

                return response.IsSuccessStatusCode ? PutPositionOutcome.Saved : PutPositionOutcome.Failed;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "CadenceConfig: Storyteller position write for {Book} failed.", bookUuid);
                return PutPositionOutcome.Failed;
            }
        }

        private HttpClient Client() => _httpClientFactory.CreateClient("CadenceConfig");

        private async Task<T?> GetJsonAsync<T>(string url, string token, CancellationToken cancellationToken)
            where T : class
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            try
            {
                using var response = await Client().SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "CadenceConfig: Storyteller GET {Url} failed.", url);
                return null;
            }
        }
    }
}
