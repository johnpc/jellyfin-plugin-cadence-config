namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>Whether a Storyteller admin login is stored, and whose. Never the token.</summary>
    public sealed class AdminLoginResult
    {
        /// <summary>Gets or sets a value indicating whether an admin token is stored.</summary>
        public bool Connected { get; set; }

        /// <summary>Gets or sets the admin username.</summary>
        public string? Username { get; set; }
    }
}
