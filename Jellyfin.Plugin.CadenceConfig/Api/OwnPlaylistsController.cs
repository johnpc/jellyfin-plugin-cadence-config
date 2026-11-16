using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Playlists;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Playlists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// GET /Cadence/Playlists/Mine — the signed-in user's OWN playlist ids, in one call. Answers only
    /// for the caller (the user comes from the verified token; there is no userId parameter), so it
    /// reveals nothing about anyone else. The selection lives in the unit-tested
    /// <see cref="OwnPlaylists.For"/>; this controller is thin plumbing.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence/Playlists")]
    [ExcludeFromCodeCoverage]
    public class OwnPlaylistsController : ControllerBase
    {
        private readonly IPlaylistManager _playlistManager;
        private readonly IAuthorizationContext _authContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="OwnPlaylistsController"/> class.
        /// </summary>
        /// <param name="playlistManager">Jellyfin's playlist manager.</param>
        /// <param name="authContext">Resolves the caller from the request's token.</param>
        public OwnPlaylistsController(IPlaylistManager playlistManager, IAuthorizationContext authContext)
        {
            _playlistManager = playlistManager;
            _authContext = authContext;
        }

        /// <summary>
        /// Gets the ids of the playlists the caller owns.
        /// </summary>
        /// <returns>The caller's own playlist ids, or 403 when the token carries no user.</returns>
        [HttpGet("Mine")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<OwnPlaylistsResult>> Mine()
        {
            var info = await _authContext.GetAuthorizationInfo(Request).ConfigureAwait(false);
            var userId = CallerScope.Resolve(info.UserId, System.Guid.Empty);
            if (userId == null)
            {
                return Forbid();
            }

            var candidates = _playlistManager.GetPlaylists(userId.Value).Select(p => (p.Id, p.OwnerUserId));
            return new OwnPlaylistsResult { Ids = OwnPlaylists.For(userId.Value, candidates) };
        }
    }
}
