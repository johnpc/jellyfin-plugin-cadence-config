using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The subset of a Readium Web Publication manifest the timeline builder reads.</summary>
    public sealed class ReadiumManifest
    {
        /// <summary>Gets or sets the metadata.</summary>
        [JsonPropertyName("metadata")]
        public ReadiumMetadata? Metadata { get; set; }

        /// <summary>Gets or sets the reading order (chapters).</summary>
        [JsonPropertyName("readingOrder")]
        public IReadOnlyList<ReadiumLink> ReadingOrder { get; set; } = Array.Empty<ReadiumLink>();

        /// <summary>Gets or sets the resources (audio chunks, SMIL, images, css).</summary>
        [JsonPropertyName("resources")]
        public IReadOnlyList<ReadiumLink> Resources { get; set; } = Array.Empty<ReadiumLink>();
    }
}
