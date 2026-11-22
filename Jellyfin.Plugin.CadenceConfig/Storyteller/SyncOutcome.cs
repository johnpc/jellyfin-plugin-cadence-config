using System;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The outcome of comparing the two sides' positions for one (user, book).</summary>
    public enum SyncOutcome
    {
        /// <summary>Storyteller is newer: write its position into Jellyfin.</summary>
        PullToJellyfin,

        /// <summary>Jellyfin is newer (or equal): leave Jellyfin alone (the event bridge pushes).</summary>
        JellyfinCurrent,

        /// <summary>Storyteller has no position for this book.</summary>
        NothingToPull,
    }
}
