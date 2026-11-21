using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>A manifest link.</summary>
    public sealed class ReadiumLink
    {
        /// <summary>Gets or sets the href (relative to the publication root).</summary>
        [JsonPropertyName("href")]
        public string Href { get; set; } = string.Empty;

        /// <summary>Gets or sets the media type.</summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>Gets or sets the title.</summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>Gets or sets the duration in seconds, when declared.</summary>
        [JsonPropertyName("duration")]
        public double? Duration { get; set; }
    }
}
