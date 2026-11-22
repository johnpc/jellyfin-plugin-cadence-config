namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>What happened when the plugin asked Storyteller to create an invite or an account.</summary>
    public enum AccountOutcome
    {
        /// <summary>Storyteller did it.</summary>
        Ok,

        /// <summary>Storyteller answered but refused (duplicate username/email, bad invite, forbidden) — try another name, not again.</summary>
        Rejected,

        /// <summary>Storyteller could not be reached or answered garbage — stop, don't retry with other names.</summary>
        Unreachable,
    }
}
