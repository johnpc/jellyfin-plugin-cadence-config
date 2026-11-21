namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>How a position write ended.</summary>
    public enum PutPositionOutcome
    {
        /// <summary>Stored (204).</summary>
        Saved,

        /// <summary>Storyteller already holds a NEWER position (409) — the caller should pull instead.</summary>
        Conflict,

        /// <summary>Any other failure.</summary>
        Failed,
    }
}
