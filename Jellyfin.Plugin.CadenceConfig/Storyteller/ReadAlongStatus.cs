using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Where a Jellyfin audiobook stands with Storyteller — ONE explicit state the client switches on,
    /// instead of "is there a uuid" / "are there clips" / "is the user linked" booleans.
    /// </summary>
    public enum ReadAlongStatus
    {
        /// <summary>The caller has not connected a Storyteller account.</summary>
        Unlinked,

        /// <summary>No Storyteller book matched this audiobook's title + author.</summary>
        Unmatched,

        /// <summary>Matched, but Storyteller has not finished aligning it yet.</summary>
        Aligning,

        /// <summary>Aligned, but the aligned audio does not line up with this Jellyfin book's files.</summary>
        Mismatch,

        /// <summary>Aligned and in step: clips are populated.</summary>
        Ready,
    }
}
