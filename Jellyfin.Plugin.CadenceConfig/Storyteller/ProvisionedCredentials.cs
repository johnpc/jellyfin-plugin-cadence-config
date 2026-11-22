namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The Storyteller login of an account the plugin created — for the admin to hand to the person. Admin-only.</summary>
    public sealed class ProvisionedCredentials
    {
        /// <summary>Gets or sets the Storyteller username.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Gets or sets the generated password.</summary>
        public string Password { get; set; } = string.Empty;
    }
}
