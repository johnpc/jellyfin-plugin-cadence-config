using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Gives a Jellyfin user a Storyteller account without anyone configuring anything per device:
    /// when a user with no link touches the bridge and an admin login is configured, the plugin
    /// invites and creates <c>jf-&lt;user&gt;</c> for them and stores the resulting link (kind
    /// <see cref="LinkKind.Provisioned"/>, password kept so the admin can hand it out). One
    /// provisioning at a time per user, so two simultaneous requests can't create two accounts.
    /// </summary>
    public sealed class UserProvisioner
    {
        private readonly StorytellerAccounts _accounts;
        private readonly ILinkWriter _links;
        private readonly Func<string> _newPassword;
        private readonly ILogger<UserProvisioner> _logger;
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="UserProvisioner"/> class with a random password source.
        /// </summary>
        /// <param name="accounts">The Storyteller account-creation client.</param>
        /// <param name="links">Where links are stored.</param>
        /// <param name="logger">The logger.</param>
        public UserProvisioner(StorytellerAccounts accounts, ILinkWriter links, ILogger<UserProvisioner> logger)
            : this(accounts, links, RandomPassword, logger)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserProvisioner"/> class.
        /// </summary>
        /// <param name="accounts">The Storyteller account-creation client.</param>
        /// <param name="links">Where links are stored.</param>
        /// <param name="newPassword">Generates a password for a new account.</param>
        /// <param name="logger">The logger.</param>
        public UserProvisioner(StorytellerAccounts accounts, ILinkWriter links, Func<string> newPassword, ILogger<UserProvisioner> logger)
        {
            _accounts = accounts;
            _links = links;
            _newPassword = newPassword;
            _logger = logger;
        }

        /// <summary>A 24-character URL-safe random password.</summary>
        /// <returns>The password.</returns>
        public static string RandomPassword()
        {
            const string alphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(24);
            var chars = new char[bytes.Length];
            for (var i = 0; i < bytes.Length; i++)
            {
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            }

            return new string(chars);
        }

        /// <summary>
        /// The user's link, creating a Storyteller account when there is none and an admin login exists.
        /// </summary>
        /// <param name="jellyfinUserId">The Jellyfin user.</param>
        /// <param name="jellyfinUsername">Their Jellyfin username (basis of the Storyteller name).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The link, or null when none exists and none could be made.</returns>
        public async Task<StorytellerLink?> EnsureLinkAsync(Guid jellyfinUserId, string jellyfinUsername, CancellationToken cancellationToken)
        {
            var existing = LinkStore.Find(_links.Current(), jellyfinUserId);
            if (existing != null)
            {
                return existing;
            }

            var gate = _locks.GetOrAdd(jellyfinUserId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return LinkStore.Find(_links.Current(), jellyfinUserId)
                    ?? await ProvisionAsync(jellyfinUserId, jellyfinUsername, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<StorytellerLink?> ProvisionAsync(Guid jellyfinUserId, string jellyfinUsername, CancellationToken cancellationToken)
        {
            var config = _links.Current();
            var baseUrl = StorytellerUrls.Base(config.StorytellerUrl);
            if (baseUrl == null || string.IsNullOrEmpty(config.StorytellerAdminToken))
            {
                return null;
            }

            for (var attempt = 1; attempt <= ProvisionNames.MaxAttempts; attempt++)
            {
                var username = ProvisionNames.For(jellyfinUsername, attempt);
                var email = ProvisionNames.EmailFor(username);
                var invite = await _accounts.CreateInviteAsync(baseUrl, config.StorytellerAdminToken, email, cancellationToken).ConfigureAwait(false);
                if (invite.Outcome == AccountOutcome.Unreachable)
                {
                    return null;
                }

                if (invite.Outcome == AccountOutcome.Rejected)
                {
                    continue;
                }

                var password = _newPassword();
                var account = await _accounts.AcceptInviteAsync(baseUrl, email, invite.Value!, username, jellyfinUsername, password, cancellationToken).ConfigureAwait(false);
                if (account.Outcome == AccountOutcome.Unreachable)
                {
                    return null;
                }

                if (account.Outcome == AccountOutcome.Rejected)
                {
                    continue;
                }

                var link = new StorytellerLink
                {
                    JellyfinUserId = jellyfinUserId,
                    Username = username,
                    Password = password,
                    Token = account.Value!,
                    Kind = LinkKind.Provisioned,
                };
                _links.Upsert(link);
                _logger.LogInformation("CadenceConfig: created Storyteller account {Storyteller} for Jellyfin user {Jellyfin}.", username, jellyfinUsername);
                return link;
            }

            _logger.LogWarning("CadenceConfig: could not find a free Storyteller username for {Jellyfin} after {Attempts} attempts.", jellyfinUsername, ProvisionNames.MaxAttempts);
            return null;
        }
    }
}
