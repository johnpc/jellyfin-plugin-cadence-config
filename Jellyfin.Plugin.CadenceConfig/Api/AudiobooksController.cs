using System;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CadenceConfig.Audiobooks;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// Serves the Cadence client its precomputed audiobook library in ONE response. Designed so NO
    /// USER EVER WAITS on a request thread:
    ///  - FRESH cache hit  → serve instantly.
    ///  - STALE cache hit  → serve the stale copy instantly + refresh in the BACKGROUND
    ///    (stale-while-revalidate); the next visit gets the fresh one.
    ///  - COLD miss        → trigger a background build and return 503 so the client falls back to
    ///    its native recursive scan THIS once (it works, just slower); the next visit is fast.
    /// The daily AudiobooksTask pre-warms every user, so cold misses are rare in practice.
    /// Authenticated — the library is the calling user's own.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence")]
    [ExcludeFromCodeCoverage]
    public class AudiobooksController : ControllerBase
    {
        private readonly AudiobooksCache _cache;
        private readonly AudiobooksRefresher _refresher;
        private readonly IUserManager _userManager;

        /// <summary>Initializes a new instance of the <see cref="AudiobooksController"/> class.</summary>
        /// <param name="cache">The per-user library cache.</param>
        /// <param name="refresher">Background (re)builder — keeps requests off the compute path.</param>
        /// <param name="userManager">Resolves the user for a background build.</param>
        public AudiobooksController(
            AudiobooksCache cache,
            AudiobooksRefresher refresher,
            IUserManager userManager)
        {
            _cache = cache;
            _refresher = refresher;
            _userManager = userManager;
        }

        /// <summary>
        /// Gets the precomputed audiobook library for the given user (never blocks on a compute).
        /// </summary>
        /// <param name="userId">The calling user's Jellyfin id (library scope).</param>
        /// <returns>The library (200), or 503 on a cold miss so the client uses its native scan.</returns>
        [HttpGet("Audiobooks")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public ActionResult<AudiobooksResult> GetAudiobooks([FromQuery] Guid userId)
        {
            var cached = _cache.Get(userId, DateTime.UtcNow);
            if (cached is { } hit)
            {
                if (hit.Stale)
                {
                    // Serve stale NOW; refresh behind the scenes for next time.
                    var staleUser = _userManager.GetUserById(userId);
                    if (staleUser is not null)
                    {
                        _ = _refresher.RefreshAsync(staleUser, () => DateTime.UtcNow);
                    }
                }

                return hit.Result;
            }

            var user = _userManager.GetUserById(userId);
            if (user is null)
            {
                return NotFound();
            }

            // Cold miss: build in the background (don't block this request) and tell the client to
            // use its native fallback this once. Its next visit hits the warm cache.
            _ = _refresher.RefreshAsync(user, () => DateTime.UtcNow);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        /// <summary>
        /// Force-regenerate the caller's audiobook library: drop their cached entry and rebuild it
        /// fresh in the background. Backs a "Refresh" action for when a user imports new books and
        /// wants them to show now rather than waiting for the daily prewarm / stale window. Returns
        /// 202 immediately — never blocks.
        /// </summary>
        /// <param name="userId">The calling user's Jellyfin id.</param>
        /// <returns>202 Accepted (rebuild kicked off), or 404 for an unknown user.</returns>
        [HttpPost("Audiobooks/Refresh")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult RefreshAudiobooks([FromQuery] Guid userId)
        {
            var user = _userManager.GetUserById(userId);
            if (user is null)
            {
                return NotFound();
            }

            _cache.Invalidate(userId);
            _ = _refresher.RefreshAsync(user, () => DateTime.UtcNow);
            return Accepted();
        }
    }
}
