using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// <c>GET /Cadence/Storyteller/Books</c> — the read-along status of every audiobook for the caller, so the
    /// client can badge the Books list and filter it. Caller from the verified token; no userId parameter.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence/Storyteller/Books")]
    [ExcludeFromCodeCoverage]
    public class ReadAlongLibraryController : ControllerBase
    {
        private readonly IAuthorizationContext _authContext;
        private readonly ReadAlongLibraryService _library;

        /// <summary>Initializes a new instance of the <see cref="ReadAlongLibraryController"/> class.</summary>
        /// <param name="authContext">Resolves the caller.</param>
        /// <param name="library">Builds the listing.</param>
        public ReadAlongLibraryController(IAuthorizationContext authContext, ReadAlongLibraryService library)
        {
            _authContext = authContext;
            _library = library;
        }

        /// <summary>Gets the read-along status of every audiobook.</summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The listing, or 403 when the token carries no user.</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ReadAlongLibrary>> Get(CancellationToken cancellationToken)
        {
            var info = await _authContext.GetAuthorizationInfo(Request).ConfigureAwait(false);
            var caller = CallerScope.Resolve(info.UserId, System.Guid.Empty);
            if (caller == null)
            {
                return Forbid();
            }

            return await _library.ForAsync(caller.Value, cancellationToken).ConfigureAwait(false);
        }
    }
}
