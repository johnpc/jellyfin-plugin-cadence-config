using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using Jellyfin.Plugin.CadenceConfig.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// Imports a public Deezer playlist into a Jellyfin playlist for the calling user, then subscribes
    /// it: the first import creates the playlist; re-importing the same Deezer playlist reuses it and
    /// only adds newly-owned tracks. It returns the artists whose tracks are still missing so the
    /// client can offer to request them via Lidarr — and once those arrive, the DeezerSyncTask keeps
    /// the playlist up to date. The work itself lives in the shared <see cref="DeezerImportService"/>;
    /// this controller is thin plumbing.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence/Deezer")]
    [ExcludeFromCodeCoverage]
    public class DeezerImportController : ControllerBase
    {
        private readonly DeezerImportService _importService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeezerImportController"/> class.
        /// </summary>
        /// <param name="importService">The shared Deezer import/sync engine.</param>
        public DeezerImportController(DeezerImportService importService)
        {
            _importService = importService;
        }

        /// <summary>
        /// Imports (and subscribes) a public Deezer playlist for the given user.
        /// </summary>
        /// <param name="userId">The calling user's Jellyfin id (playlist owner + library scope).</param>
        /// <param name="url">A Deezer playlist share URL or bare id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The created/updated playlist + missing artists, or an error status.</returns>
        [HttpPost("Import")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<DeezerImportResult>> Import(
            [FromQuery] Guid userId,
            [FromQuery] string? url,
            CancellationToken cancellationToken)
        {
            var result = await _importService.ImportAsync(userId, url, cancellationToken).ConfigureAwait(false);
            if (result == null)
            {
                return BadRequest("Could not read that Deezer playlist (private, not found, or bad URL).");
            }

            return result;
        }
    }
}
