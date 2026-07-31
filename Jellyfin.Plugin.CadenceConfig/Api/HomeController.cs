using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CadenceConfig.Home;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// Serves the Cadence client its precomputed Home shelves in ONE response, so the client skips
    /// ~6 slow recursive library scans per Home load. Reads the scheduled task's cache when warm;
    /// on a miss (task hasn't run for this user yet) it computes on demand so the first load still
    /// works. Authenticated — the shelves are the calling user's own library.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence")]
    [ExcludeFromCodeCoverage]
    public class HomeController : ControllerBase
    {
        private readonly HomeShelvesCache _cache;
        private readonly HomeShelvesService _service;
        private readonly IUserManager _userManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeController"/> class.
        /// </summary>
        /// <param name="cache">The per-user shelves cache.</param>
        /// <param name="service">The shelves compute service (on-demand cache miss).</param>
        /// <param name="userManager">Resolves the user for an on-demand compute.</param>
        public HomeController(HomeShelvesCache cache, HomeShelvesService service, IUserManager userManager)
        {
            _cache = cache;
            _service = service;
            _userManager = userManager;
        }

        /// <summary>
        /// Gets the precomputed Home shelves for the given user.
        /// </summary>
        /// <param name="userId">The calling user's Jellyfin id (library scope).</param>
        /// <returns>The Home shelves, or 404 when the user is unknown.</returns>
        [HttpGet("Home")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<HomeShelvesResult> GetHome([FromQuery] System.Guid userId)
        {
            var cached = _cache.Get(userId);
            if (cached is not null)
            {
                return cached;
            }

            var user = _userManager.GetUserById(userId);
            if (user is null)
            {
                return NotFound();
            }

            // Cold cache: compute now (still faster than the client's 6 tunneled calls) and store
            // so the next request is instant.
            var result = _service.Build(user);
            _cache.Set(userId, result);
            return result;
        }
    }
}
