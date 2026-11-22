namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>Body of the admin login (and of mapping a user to an existing account): a Storyteller username + password.</summary>
    public sealed class AdminLoginRequest
    {
        /// <summary>Gets or sets the Storyteller username (or email).</summary>
        public string? Username { get; set; }

        /// <summary>Gets or sets the password (used once to obtain a token; never stored).</summary>
        public string? Password { get; set; }
    }
}
