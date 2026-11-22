using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// The two Storyteller calls that create a user: an admin creates an invite for an email, then the
    /// invite is accepted with a username and password, which also returns the new account's token.
    /// Kept apart from <see cref="StorytellerClient"/> (which only ever acts as a linked user).
    /// </summary>
    public sealed class StorytellerAccounts
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<StorytellerAccounts> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorytellerAccounts"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The Jellyfin-provided HTTP client factory.</param>
        /// <param name="logger">The logger.</param>
        public StorytellerAccounts(IHttpClientFactory httpClientFactory, ILogger<StorytellerAccounts> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>Creates a reader invite (book read/list/download only) as the admin.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="adminToken">The admin's bearer token.</param>
        /// <param name="email">The invitee's email (Storyteller keys invites by it).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The invite key on success.</returns>
        public async Task<AccountResult> CreateInviteAsync(string baseUrl, string adminToken, string email, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(StorytellerUrls.Invites(baseUrl)));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            request.Content = JsonContent.Create(new InviteRequest { Email = email }, options: Json);
            return await SendAsync<StorytellerInvite>(request, r => r.Key, "invite", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Accepts an invite: creates the account and returns its bearer token.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="email">The invited email.</param>
        /// <param name="inviteKey">The invite key.</param>
        /// <param name="username">The new account's username.</param>
        /// <param name="name">The new account's display name.</param>
        /// <param name="password">The new account's password.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The new account's token on success.</returns>
        public async Task<AccountResult> AcceptInviteAsync(string baseUrl, string email, string inviteKey, string username, string name, string password, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(StorytellerUrls.Users(baseUrl)));
            request.Content = JsonContent.Create(
                new AcceptRequest { Email = email, InviteKey = inviteKey, Username = username, Name = name, Password = password },
                options: Json);
            return await SendAsync<StorytellerToken>(request, r => r.AccessToken, "account", cancellationToken).ConfigureAwait(false);
        }

        private async Task<AccountResult> SendAsync<T>(HttpRequestMessage request, Func<T, string?> value, string what, CancellationToken cancellationToken)
            where T : class
        {
            try
            {
                using var response = await _httpClientFactory.CreateClient("CadenceConfig").SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("CadenceConfig: Storyteller refused to create {What} ({Status}).", what, (int)response.StatusCode);
                    return new AccountResult(AccountOutcome.Rejected, null);
                }

                var body = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false);
                var result = body == null ? null : value(body);
                return string.IsNullOrEmpty(result)
                    ? new AccountResult(AccountOutcome.Unreachable, null)
                    : new AccountResult(AccountOutcome.Ok, result);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "CadenceConfig: Storyteller {What} creation failed.", what);
                return new AccountResult(AccountOutcome.Unreachable, null);
            }
        }

        /// <summary>The invite body: an email plus the reader permission set (everything else stays false).</summary>
        private sealed class InviteRequest
        {
            [JsonPropertyName("email")]
            public string Email { get; set; } = string.Empty;

            [JsonPropertyName("bookRead")]
            public bool BookRead { get; set; } = true;

            [JsonPropertyName("bookList")]
            public bool BookList { get; set; } = true;

            [JsonPropertyName("bookDownload")]
            public bool BookDownload { get; set; } = true;
        }

        private sealed class AcceptRequest
        {
            [JsonPropertyName("email")]
            public string Email { get; set; } = string.Empty;

            [JsonPropertyName("inviteKey")]
            public string InviteKey { get; set; } = string.Empty;

            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("password")]
            public string Password { get; set; } = string.Empty;
        }
    }
}
