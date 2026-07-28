using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Chapters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// Serves embedded audiobook chapters for a Jellyfin audio item — the piece Jellyfin's own item API
    /// omits for audio files. Authenticated (any signed-in user). The extraction/parsing lives in the
    /// unit-tested <see cref="FfprobeChapters"/> + <see cref="ChapterService"/>; this controller is thin
    /// plumbing.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence/Audiobooks")]
    [ExcludeFromCodeCoverage]
    public class ChaptersController : ControllerBase
    {
        private readonly ChapterService _chapterService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChaptersController"/> class.
        /// </summary>
        /// <param name="chapterService">The chapter extraction service.</param>
        public ChaptersController(ChapterService chapterService)
        {
            _chapterService = chapterService;
        }

        /// <summary>
        /// Get the chapters for an audiobook item. Returns an empty array when the file has none, and
        /// 404 when the item id doesn't resolve to a probeable file.
        /// </summary>
        /// <param name="itemId">The Jellyfin item id of the audiobook.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The chapters for the item.</returns>
        [HttpGet("{itemId}/Chapters")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IReadOnlyList<AudiobookChapter>>> GetChapters(
            [FromRoute] Guid itemId,
            CancellationToken cancellationToken)
        {
            var chapters = await _chapterService.GetChaptersAsync(itemId, cancellationToken).ConfigureAwait(false);
            return chapters is null ? NotFound() : Ok(chapters);
        }
    }
}
