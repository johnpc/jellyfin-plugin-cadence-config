using System;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CadenceConfig.Home;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// Serves the Cadence client its precomputed Home shelves in ONE response. Designed so NO USER
    /// EVER WAITS on a request thread:
    ///  - FRESH cache hit  → serve instantly.
    ///  - STALE cache hit  → serve the stale copy instantly + refresh in the BACKGROUND
    ///    (stale-while-revalidate); the next visit gets the fresh one.
    ///  - COLD miss        → trigger a background build and return 503 so the client falls back to
    ///    its native per-shelf queries THIS once (they work, just slower); the next visit is fast.
    /// The daily HomeShelvesTask pre-warms every user, so cold misses are rare in practice.
    /// Authenticated — the shelves are the calling user's own library.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence")]
    [ExcludeFromCodeCoverage]
    public class HomeController : ControllerBase
    {
        private readonly HomeShelvesCache _cache;
        private readonly HomeShelvesRefresher _refresher;
        private readonly IUserManager _userManager;

        /// <summary>Initializes a new instance of the <see cref="HomeController"/> class.</summary>
        /// <param name="cache">The per-user shelves cache.</param>
        /// <param name="refresher">Background (re)builder — keeps requests off the compute path.</param>
        /// <param name="userManager">Resolves the user for a background build.</param>
        public HomeController(
            HomeShelvesCache cache,
            HomeShelvesRefresher refresher,
            IUserManager userManager)
        {
            _cache = cache;
            _refresher = refresher;
            _userManager = userManager;
        }

        /// <summary>
        /// Gets the precomputed Home shelves for the given user (never blocks on a compute).
        /// </summary>
        /// <param name="userId">The calling user's Jellyfin id (library scope).</param>
        /// <returns>The shelves (200), or 503 on a cold miss so the client uses native queries.</returns>
        [HttpGet("Home")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public ActionResult<HomeShelvesResult> GetHome([FromQuery] Guid userId)
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
    }
}
