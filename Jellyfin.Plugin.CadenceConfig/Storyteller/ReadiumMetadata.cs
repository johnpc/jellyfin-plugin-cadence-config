using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>Manifest metadata.</summary>
    public sealed class ReadiumMetadata
    {
        /// <summary>Gets or sets the title.</summary>
        [JsonPropertyName("title")]
        public object? Title { get; set; }

        /// <summary>Gets or sets the total duration in seconds, when the publication declares it.</summary>
        [JsonPropertyName("duration")]
        public double? Duration { get; set; }
    }
}
