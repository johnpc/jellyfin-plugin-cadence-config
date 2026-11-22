using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// The server maintainer's side of Storyteller: store an admin login (so users get accounts
    /// created automatically), see every Jellyfin user's link, map a user to an existing Storyteller
    /// account, create one now, reveal a created account's login, or unlink. Jellyfin admins only.
    /// No endpoint ever returns a token. Decisions live in <see cref="AdminLinks"/>; this is plumbing.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "RequiresElevation")]
    [Route("Cadence/Storyteller/Admin")]
    [ExcludeFromCodeCoverage]
    public class StorytellerAdminController : ControllerBase
    {
        private readonly StorytellerClient _client;
        private readonly UserProvisioner _provisioner;
        private readonly ILinkWriter _links;
        private readonly IUserManager _users;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorytellerAdminController"/> class.
        /// </summary>
        /// <param name="client">The Storyteller client (logins).</param>
        /// <param name="provisioner">Creates accounts.</param>
        /// <param name="links">Where links and the admin login are stored.</param>
        /// <param name="users">Jellyfin's users.</param>
        public StorytellerAdminController(StorytellerClient client, UserProvisioner provisioner, ILinkWriter links, IUserManager users)
        {
            _client = client;
            _provisioner = provisioner;
            _links = links;
            _users = users;
        }

        /// <summary>Stores the Storyteller admin login (exchanged for a token server-side).</summary>
        /// <param name="body">Username + password.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>200 connected · 401 bad credentials · 404 no Storyteller URL · 502 unreachable.</returns>
        [HttpPost("Login")]
        public async Task<ActionResult<AdminLoginResult>> Login([FromBody] AdminLoginRequest body, CancellationToken cancellationToken)
        {
            var config = _links.Current();
            var baseUrl = StorytellerUrls.Base(config.StorytellerUrl);
            if (baseUrl == null)
            {
                return NotFound();
            }

            var login = await _client.LoginAsync(baseUrl, body.Username ?? string.Empty, body.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (login.Outcome != LoginOutcome.Ok)
            {
                return StatusCode(login.Outcome == LoginOutcome.BadCredentials ? StatusCodes.Status401Unauthorized : StatusCodes.Status502BadGateway);
            }

            config.StorytellerAdminUsername = body.Username ?? string.Empty;
            config.StorytellerAdminToken = login.Token!;
            _links.Save();
            return new AdminLoginResult { Connected = true, Username = body.Username };
        }

        /// <summary>Forgets the admin login (no more automatic accounts).</summary>
        /// <returns>204.</returns>
        [HttpDelete("Login")]
        public IActionResult Logout()
        {
            var config = _links.Current();
            config.StorytellerAdminUsername = string.Empty;
            config.StorytellerAdminToken = string.Empty;
            _links.Save();
            return NoContent();
        }

        /// <summary>Every Jellyfin user with their link state, plus whether an admin login is stored.</summary>
        /// <returns>The rows.</returns>
        [HttpGet("Users")]
        public ActionResult<AdminUsersResult> Users()
        {
            var config = _links.Current();
            var rows = AdminLinks.Rows(_users.GetUsers().Select(u => (u.Id, u.Username)).OrderBy(u => u.Username), config);
            return new AdminUsersResult
            {
                AdminConnected = !string.IsNullOrEmpty(config.StorytellerAdminToken),
                AdminUsername = string.IsNullOrEmpty(config.StorytellerAdminUsername) ? null : config.StorytellerAdminUsername,
                Users = rows,
            };
        }

        /// <summary>Maps a Jellyfin user to an EXISTING Storyteller account (password used once, not stored).</summary>
        /// <param name="userId">The Jellyfin user.</param>
        /// <param name="body">The Storyteller username + password.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>200 the row · 401 · 404 · 502.</returns>
        [HttpPut("Users/{userId:guid}")]
        public async Task<ActionResult<AdminUserRow>> Map(Guid userId, [FromBody] AdminLoginRequest body, CancellationToken cancellationToken)
        {
            var user = _users.GetUserById(userId);
            var baseUrl = StorytellerUrls.Base(_links.Current().StorytellerUrl);
            if (user == null || baseUrl == null)
            {
                return NotFound();
            }

            var login = await _client.LoginAsync(baseUrl, body.Username ?? string.Empty, body.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (login.Outcome != LoginOutcome.Ok)
            {
                return StatusCode(login.Outcome == LoginOutcome.BadCredentials ? StatusCodes.Status401Unauthorized : StatusCodes.Status502BadGateway);
            }

            var link = AdminLinks.Mapped(userId, body.Username ?? string.Empty, login.Token!);
            _links.Upsert(link);
            return AdminLinks.RowFor(userId, user.Username, link);
        }

        /// <summary>Creates a Storyteller account for a Jellyfin user now and returns its login for the admin to pass on.</summary>
        /// <param name="userId">The Jellyfin user.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>200 credentials · 404 · 409 already linked · 412 no admin login · 502.</returns>
        [HttpPost("Users/{userId:guid}/Provision")]
        public async Task<ActionResult<ProvisionedCredentials>> Provision(Guid userId, CancellationToken cancellationToken)
        {
            var user = _users.GetUserById(userId);
            if (user == null)
            {
                return NotFound();
            }

            if (LinkStore.Find(_links.Current(), userId) != null)
            {
                return Conflict();
            }

            if (string.IsNullOrEmpty(_links.Current().StorytellerAdminToken))
            {
                return StatusCode(StatusCodes.Status412PreconditionFailed);
            }

            var link = await _provisioner.EnsureLinkAsync(userId, user.Username, cancellationToken).ConfigureAwait(false);
            var credentials = AdminLinks.Credentials(link);
            return credentials == null ? StatusCode(StatusCodes.Status502BadGateway) : credentials;
        }

        /// <summary>The login of an account the plugin created (404 for mapped or unlinked users).</summary>
        /// <param name="userId">The Jellyfin user.</param>
        /// <returns>The credentials.</returns>
        [HttpGet("Users/{userId:guid}/Credentials")]
        public ActionResult<ProvisionedCredentials> Credentials(Guid userId)
        {
            var credentials = AdminLinks.Credentials(LinkStore.Find(_links.Current(), userId));
            return credentials == null ? NotFound() : credentials;
        }

        /// <summary>Removes a user's link (their Storyteller account itself is untouched).</summary>
        /// <param name="userId">The Jellyfin user.</param>
        /// <returns>204.</returns>
        [HttpDelete("Users/{userId:guid}")]
        public IActionResult Unlink(Guid userId)
        {
            _links.Remove(userId);
            return NoContent();
        }
    }
}
