using System;
using System.Linq;
using Jellyfin.Plugin.CadenceConfig.Configuration;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Where a caller stands with Storyteller — ONE explicit state instead of "is the URL set" plus
    /// "is there a token" booleans that could disagree.
    /// </summary>
    public enum LinkState
    {
        /// <summary>The operator has not configured a Storyteller URL; the feature is off.</summary>
        Unconfigured,

        /// <summary>Storyteller is configured but this user has not connected their account.</summary>
        Unlinked,

        /// <summary>This user has a stored Storyteller token.</summary>
        Linked,
    }
}
