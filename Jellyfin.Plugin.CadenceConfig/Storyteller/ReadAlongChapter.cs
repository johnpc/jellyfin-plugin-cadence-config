using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>A chapter of the readaloud publication (index = <see cref="ReadAlongClip.Chapter"/>).</summary>
    public sealed class ReadAlongChapter
    {
        /// <summary>Gets or sets the chapter href inside the publication.</summary>
        public string Href { get; set; } = string.Empty;

        /// <summary>Gets or sets the chapter title.</summary>
        public string? Title { get; set; }
    }
}
