using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>Storyteller's stored position: a locator plus the client's wall-clock stamp.</summary>
    public sealed class StorytellerPosition
    {
        /// <summary>Gets or sets the locator.</summary>
        [JsonPropertyName("locator")]
        public ReadiumLocator Locator { get; set; } = new();

        /// <summary>Gets or sets the timestamp (ms since epoch). Newer wins.</summary>
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }
    }
}
