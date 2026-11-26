namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>Whether the caller's library can be checked for read-along at all.</summary>
    public enum LibraryState
    {
        /// <summary>Checked: every audiobook carries its own status.</summary>
        Ok,

        /// <summary>The caller has no Storyteller account and none can be created (no admin login stored).</summary>
        Unlinked,

        /// <summary>No Storyteller URL is configured on this server.</summary>
        Unconfigured,
    }
}
