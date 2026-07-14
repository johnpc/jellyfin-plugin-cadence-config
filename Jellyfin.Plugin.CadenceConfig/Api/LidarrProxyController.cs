using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Lidarr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// Proxies the Cadence client's "request missing music" calls to Lidarr, injecting the API key
    /// SERVER-SIDE so the write-capable credential never reaches the browser or the native app.
    /// Authenticated (any signed-in Jellyfin user — requesting is open to all) and restricted to a
    /// curated allowlist (<see cref="LidarrProxyPolicy"/>) so delete/command/config paths stay
    /// unreachable. The client calls <c>/Cadence/Lidarr/&lt;sub&gt;</c>; this forwards to
    /// <c>&lt;LidarrUrl&gt;/api/v1/&lt;sub&gt;</c>. The forward/refuse decision lives in the pure
    /// <see cref="LidarrProxyPlan"/> (unit-tested); this controller is thin HTTP plumbing only.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence/Lidarr")]
    [ExcludeFromCodeCoverage]
    public class LidarrProxyController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LidarrProxyController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LidarrProxyController"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The Jellyfin-provided HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public LidarrProxyController(IHttpClientFactory httpClientFactory, ILogger<LidarrProxyController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Forwards an allowlisted GET (search, list profiles/root folders, poll the queue, look up
        /// an artist/album) to Lidarr.
        /// </summary>
        /// <param name="subPath">The Lidarr v1 sub-path after the proxy prefix.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Lidarr's response, passed through.</returns>
        [HttpGet("{**subPath}")]
        public Task<IActionResult> Get(string subPath, CancellationToken cancellationToken)
        {
            return ForwardAsync(HttpMethod.Get, subPath, cancellationToken);
        }

        /// <summary>
        /// Forwards an allowlisted POST (add an artist/album) to Lidarr, passing the JSON body through.
        /// </summary>
        /// <param name="subPath">The Lidarr v1 sub-path after the proxy prefix.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Lidarr's response, passed through.</returns>
        [HttpPost("{**subPath}")]
        public Task<IActionResult> Post(string subPath, CancellationToken cancellationToken)
        {
            return ForwardAsync(HttpMethod.Post, subPath, cancellationToken);
        }

        private async Task<IActionResult> ForwardAsync(HttpMethod method, string subPath, CancellationToken cancellationToken)
        {
            var relative = LidarrUpstream.JoinQuery(subPath, Request.QueryString.Value);
            var plan = LidarrProxyPlan.Create(method.Method, relative, Plugin.GetConfiguration());
            if (!plan.Forward)
            {
                if (plan.StatusCode == StatusCodes.Status403Forbidden)
                {
                    _logger.LogWarning("CadenceConfig: refused Lidarr proxy for {Method} {Path} (not allowlisted).", method.Method, subPath);
                }

                return StatusCode(plan.StatusCode);
            }

            return await SendAsync(method, plan.TargetUrl!, plan.ApiKey!, cancellationToken).ConfigureAwait(false);
        }

        private async Task<IActionResult> SendAsync(HttpMethod method, string target, string apiKey, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(method, target);
            request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);

            if (method == HttpMethod.Post)
            {
                using var reader = new System.IO.StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("CadenceConfig");
                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = payload,
                    ContentType = mediaType,
                };
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "CadenceConfig: Lidarr proxy request to {Target} failed.", target);
                return StatusCode(StatusCodes.Status502BadGateway);
            }
        }
    }
}
