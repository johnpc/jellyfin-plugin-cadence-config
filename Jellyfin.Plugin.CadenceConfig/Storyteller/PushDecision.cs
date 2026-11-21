using System;
using System.Collections.Concurrent;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>What to do with one user-data save — ONE explicit decision.</summary>
    public enum PushDecision
    {
        /// <summary>Send this position to Storyteller now.</summary>
        Push,

        /// <summary>Too soon after the last push for this (user, book); wait for the next save.</summary>
        Throttled,

        /// <summary>Not a position save we bridge (a rating, a favorite toggle, our own write…).</summary>
        Ignore,
    }
}
