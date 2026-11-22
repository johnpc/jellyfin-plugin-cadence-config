using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>A Jellyfin part as the plugin sees it (the shared source of part offsets).</summary>
    public sealed class ReadAlongPart
    {
        /// <summary>Gets or sets the Jellyfin item id.</summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>Gets or sets the part length in seconds.</summary>
        public double Duration { get; set; }
    }
}
