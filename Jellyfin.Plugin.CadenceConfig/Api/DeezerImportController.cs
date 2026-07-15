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

        /// <summary>
        /// Gets the current missing artists for a Deezer-mirrored Jellyfin playlist, recomputed against
        /// the user's library so an artist Lidarr has filled in since import is already gone. Lets the
        /// client show a persistent "request these" list on the playlist page across sessions.
        /// </summary>
        /// <param name="userId">The calling user's Jellyfin id (must own the subscription).</param>
        /// <param name="playlistId">The Jellyfin playlist id shown on the client.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The subscription status, or 404 when the playlist isn't a Deezer subscription.</returns>
        [HttpGet("Subscription")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DeezerSubscriptionStatus>> Subscription(
            [FromQuery] Guid userId,
            [FromQuery] string? playlistId,
            CancellationToken cancellationToken)
        {
            var status = await _importService
                .GetMissingArtistsAsync(userId, playlistId ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
            if (status == null)
            {
                return NotFound();
            }

            return status;
        }
    }
}
