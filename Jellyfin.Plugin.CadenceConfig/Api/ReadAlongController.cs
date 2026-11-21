using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
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
    /// <c>/Cadence/Storyteller/Books/{itemId}</c>: what the Cadence client needs for read-along — the
    /// book's status + sentence timeline, the chapter text (proxied with the caller's Storyteller token,
    /// which never reaches the client), and the merged reading position. Caller-scoped throughout.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence/Storyteller/Books/{itemId:guid}")]
    [ExcludeFromCodeCoverage]
    public class ReadAlongController : ControllerBase
    {
        private readonly IAuthorizationContext _authContext;
        private readonly ReadAlongService _readAlong;
        private readonly PositionService _positions;
        private readonly StorytellerClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadAlongController"/> class.
        /// </summary>
        /// <param name="authContext">Resolves the caller from the request's token.</param>
        /// <param name="readAlong">Resolves books.</param>
        /// <param name="positions">Merges/pushes positions.</param>
        /// <param name="client">The Storyteller client (text proxy).</param>
        public ReadAlongController(IAuthorizationContext authContext, ReadAlongService readAlong, PositionService positions, StorytellerClient client)
        {
            _authContext = authContext;
            _readAlong = readAlong;
            _positions = positions;
            _client = client;
        }

        /// <summary>The book's read-along status, parts, chapters and clips.</summary>
        /// <param name="itemId">Any part of the book.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The book, or 404 when it isn't an audiobook / Storyteller isn't configured.</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ReadAlongBook>> Get(Guid itemId, CancellationToken cancellationToken)
        {
            var resolved = await ResolveAsync(itemId, cancellationToken).ConfigureAwait(false);
            return resolved == null ? NotFound() : ReadAlongService.ToResponse(resolved);
        }

        /// <summary>Streams a resource of the aligned publication (chapter XHTML, image, css).</summary>
        /// <param name="itemId">Any part of the book.</param>
        /// <param name="path">The publication-relative path.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The upstream body with its content type (Range honoured).</returns>
        [HttpGet("Text/{**path}")]
        public async Task<IActionResult> Text(Guid itemId, string path, CancellationToken cancellationToken)
        {
            var resolved = await ResolveAsync(itemId, cancellationToken).ConfigureAwait(false);
            if (resolved?.Book == null || resolved.Status == ReadAlongStatus.Unlinked)
            {
                return NotFound();
            }

            var range = RangeHeaderValue.TryParse(Request.Headers.Range.ToString(), out var parsed) ? parsed : null;
            var upstream = await _client.GetResourceAsync(resolved.BaseUrl, resolved.Link.Token, resolved.Book.Uuid, path, range, cancellationToken).ConfigureAwait(false);
            if (!upstream.IsSuccessStatusCode)
            {
                upstream.Dispose();
                return StatusCode((int)upstream.StatusCode);
            }

            Response.StatusCode = (int)upstream.StatusCode;
            if (upstream.Content.Headers.ContentRange != null)
            {
                Response.Headers.ContentRange = upstream.Content.Headers.ContentRange.ToString();
            }

            Response.Headers.AcceptRanges = "bytes";
            var stream = await upstream.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            HttpContext.Response.RegisterForDispose(upstream);
            return File(stream, upstream.Content.Headers.ContentType?.ToString() ?? "application/octet-stream", enableRangeProcessing: false);
        }

        /// <summary>The newer of Storyteller's and Jellyfin's position for the book.</summary>
        /// <param name="itemId">Any part of the book.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The position, or 204 when neither side has one.</returns>
        [HttpGet("Position")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult<ReadAlongPositionResult>> GetPosition(Guid itemId, CancellationToken cancellationToken)
        {
            var resolved = await ResolveAsync(itemId, cancellationToken).ConfigureAwait(false);
            if (resolved == null)
            {
                return NotFound();
            }

            var theirs = resolved.Status == ReadAlongStatus.Ready
                ? await _positions.StorytellerPositionAsync(resolved, cancellationToken).ConfigureAwait(false)
                : null;
            var merged = PositionService.Merge(theirs, _positions.JellyfinPosition(resolved));
            if (merged == null)
            {
                return NoContent();
            }

            return new ReadAlongPositionResult { Seconds = merged.Seconds, Timestamp = merged.Timestamp, Source = merged.Source };
        }

        /// <summary>Pushes the caller's position to Storyteller.</summary>
        /// <param name="itemId">Any part of the book.</param>
        /// <param name="body">Global seconds + stamp.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>204 saved; 409 Storyteller is newer; 404 not ready.</returns>
        [HttpPut("Position")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> PutPosition(Guid itemId, [FromBody] ReadAlongPositionRequest body, CancellationToken cancellationToken)
        {
            var resolved = await ResolveAsync(itemId, cancellationToken).ConfigureAwait(false);
            if (resolved == null || resolved.Status != ReadAlongStatus.Ready)
            {
                return NotFound();
            }

            var outcome = await _positions.PushAsync(resolved, body.Seconds, body.Timestamp, cancellationToken).ConfigureAwait(false);
            return outcome switch
            {
                PutPositionOutcome.Saved => NoContent(),
                PutPositionOutcome.Conflict => Conflict(),
                _ => StatusCode(StatusCodes.Status502BadGateway),
            };
        }

        private async Task<ResolvedBook?> ResolveAsync(Guid itemId, CancellationToken cancellationToken)
        {
            var info = await _authContext.GetAuthorizationInfo(Request).ConfigureAwait(false);
            var caller = CallerScope.Resolve(info.UserId, Guid.Empty);
            return caller == null ? null : await _readAlong.ResolveAsync(caller.Value, itemId, cancellationToken).ConfigureAwait(false);
        }
    }
}
