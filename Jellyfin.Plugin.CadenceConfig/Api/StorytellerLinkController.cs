using System;
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
    /// <c>/Cadence/Storyteller/Link</c>: a signed-in user connects (or disconnects) THEIR OWN Storyteller
    /// account. The password is exchanged server-side for a Storyteller token that is stored in plugin
    /// configuration and used only inside the server; the client only ever learns linked/unlinked and
    /// the username. Caller-scoped — there is no userId parameter.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence/Storyteller/Link")]
    [ExcludeFromCodeCoverage]
    public class StorytellerLinkController : ControllerBase
    {
        private readonly IAuthorizationContext _authContext;
        private readonly StorytellerClient _client;
        private readonly BookCatalog _catalog;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorytellerLinkController"/> class.
        /// </summary>
        /// <param name="authContext">Resolves the caller from the request's token.</param>
        /// <param name="client">The Storyteller client.</param>
        /// <param name="catalog">The cached catalog (dropped for a replaced token).</param>
        public StorytellerLinkController(IAuthorizationContext authContext, StorytellerClient client, BookCatalog catalog)
        {
            _authContext = authContext;
            _client = client;
            _catalog = catalog;
        }

        /// <summary>Gets whether the caller is linked, and as whom.</summary>
        /// <returns>The link state.</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<StorytellerLinkResult>> Get()
        {
            var caller = await CallerAsync().ConfigureAwait(false);
            if (caller == null)
            {
                return Forbid();
            }

            var link = LinkStore.Find(Plugin.GetConfiguration(), caller.Value);
            return new StorytellerLinkResult { Linked = link != null, Username = link?.Username };
        }

        /// <summary>Connects the caller's Storyteller account.</summary>
        /// <param name="body">Username + password.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>200 linked; 401 bad credentials; 404 Storyteller not configured; 502 unreachable.</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<StorytellerLinkResult>> Post([FromBody] StorytellerLinkRequest body, CancellationToken cancellationToken)
        {
            var caller = await CallerAsync().ConfigureAwait(false);
            if (caller == null)
            {
                return Forbid();
            }

            var plugin = Plugin.Instance;
            var config = Plugin.GetConfiguration();
            var baseUrl = StorytellerUrls.Base(config.StorytellerUrl);
            if (baseUrl == null || plugin == null)
            {
                return NotFound();
            }

            var login = await _client.LoginAsync(baseUrl, body.Username ?? string.Empty, body.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
            switch (login.Outcome)
            {
                case LoginOutcome.BadCredentials:
                    return Unauthorized();
                case LoginOutcome.Unreachable:
                    return StatusCode(StatusCodes.Status502BadGateway);
            }

            var previous = LinkStore.Find(config, caller.Value);
            if (previous != null)
            {
                _catalog.Forget(previous.Token);
            }

            config.StorytellerLinks = LinkStore.Upsert(config.StorytellerLinks, new StorytellerLink
            {
                JellyfinUserId = caller.Value,
                Username = body.Username ?? string.Empty,
                Token = login.Token!,
            });
            plugin.SaveConfiguration();
            return new StorytellerLinkResult { Linked = true, Username = body.Username };
        }

        /// <summary>Disconnects the caller's Storyteller account.</summary>
        /// <returns>204.</returns>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Delete()
        {
            var caller = await CallerAsync().ConfigureAwait(false);
            if (caller == null)
            {
                return Forbid();
            }

            var plugin = Plugin.Instance;
            var config = Plugin.GetConfiguration();
            var previous = LinkStore.Find(config, caller.Value);
            if (previous != null)
            {
                _catalog.Forget(previous.Token);
            }

            config.StorytellerLinks = LinkStore.Remove(config.StorytellerLinks, caller.Value);
            plugin?.SaveConfiguration();
            return NoContent();
        }

        private async Task<Guid?> CallerAsync()
        {
            var info = await _authContext.GetAuthorizationInfo(Request).ConfigureAwait(false);
            return CallerScope.Resolve(info.UserId, Guid.Empty);
        }
    }
}
