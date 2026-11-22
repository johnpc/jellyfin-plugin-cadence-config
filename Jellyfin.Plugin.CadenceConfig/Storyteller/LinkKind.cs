namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// How a Jellyfin user came to have a Storyteller account on this bridge. One explicit value, not
    /// "HasPassword" booleans: it decides what the admin may see and what Cadence may assume.
    /// </summary>
    public enum LinkKind
    {
        /// <summary>The admin mapped the user to an EXISTING Storyteller account; the password was used once and never stored.</summary>
        Mapped = 0,

        /// <summary>The plugin created the Storyteller account for this user; the generated password is kept so the admin can hand it out.</summary>
        Provisioned = 1,
    }
}
