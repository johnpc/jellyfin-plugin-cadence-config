using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CadenceConfig.Audiobooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// Serves the Cadence client the whole audiobook library in one call, from a server-side cache — so
    /// the client skips its slow recursive AudioBook scan. Authenticated (any signed-in Jellyfin user).
    /// The build/cache lives in the <see cref="AudiobookLibraryService"/>; this controller is thin
    /// plumbing. Note the more specific <c>{itemId}/Chapters</c> route on ChaptersController still
    /// resolves — this endpoint is the collectionless <c>GET /Cadence/Audiobooks</c>.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence/Audiobooks")]
    [ExcludeFromCodeCoverage]
    public class AudiobookLibraryController : ControllerBase
    {
        private readonly AudiobookLibraryService _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudiobookLibraryController"/> class.
        /// </summary>
        /// <param name="service">The cached audiobook-library service.</param>
        public AudiobookLibraryController(AudiobookLibraryService service)
        {
            _service = service;
        }

        /// <summary>
        /// Gets the whole audiobook library (Type = AudioBook), SortName ascending, from cache. Only
        /// the static catalog is served; the client overlays live reading progress from its own query.
        /// </summary>
        /// <returns>The audiobook library.</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<AudiobookLibraryResult> GetAudiobooks()
        {
            return new AudiobookLibraryResult { Books = _service.GetLibrary() };
        }
    }
}
