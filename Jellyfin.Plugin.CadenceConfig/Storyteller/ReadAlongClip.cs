using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>One narrated sentence: global audio seconds ↔ a text fragment id in a chapter.</summary>
    public sealed class ReadAlongClip
    {
        /// <summary>Gets or sets the global start (seconds into the whole book).</summary>
        public double Start { get; set; }

        /// <summary>Gets or sets the global end.</summary>
        public double End { get; set; }

        /// <summary>Gets or sets the chapter index.</summary>
        public int Chapter { get; set; }

        /// <summary>Gets or sets the element id in the chapter's XHTML.</summary>
        public string Fragment { get; set; } = string.Empty;
    }
}
