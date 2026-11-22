using System;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Pure "which side wins" rule for the pull task: Storyteller's position timestamp (ms epoch) vs
    /// Jellyfin's LastPlayedDate for the book. Positions within <see cref="Slack"/> of each other are
    /// treated as the same write bouncing back, so the bridge can never ping-pong.
    /// </summary>
    public static class SyncDecision
    {
        /// <summary>Two stamps closer than this are "the same moment".</summary>
        public static readonly TimeSpan Slack = TimeSpan.FromSeconds(2);

        /// <summary>Decides.</summary>
        /// <param name="storytellerTimestampMs">Storyteller's stamp, or null when it has none.</param>
        /// <param name="jellyfinLastPlayed">Jellyfin's last-played stamp for the book, or null.</param>
        /// <returns>The outcome.</returns>
        public static SyncOutcome Decide(long? storytellerTimestampMs, DateTime? jellyfinLastPlayed)
        {
            if (storytellerTimestampMs == null)
            {
                return SyncOutcome.NothingToPull;
            }

            var theirs = DateTimeOffset.FromUnixTimeMilliseconds(storytellerTimestampMs.Value);
            if (jellyfinLastPlayed == null)
            {
                return SyncOutcome.PullToJellyfin;
            }

            var ours = new DateTimeOffset(DateTime.SpecifyKind(jellyfinLastPlayed.Value, DateTimeKind.Utc));
            return theirs - ours > Slack ? SyncOutcome.PullToJellyfin : SyncOutcome.JellyfinCurrent;
        }
    }
}
