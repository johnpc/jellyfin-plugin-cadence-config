using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The <c>locations</c> block of a Readium locator.</summary>
    public sealed class ReadiumLocations
    {
        /// <summary>Gets or sets the fragments (an element id, or <c>t=12.3</c> for audio).</summary>
        [JsonPropertyName("fragments")]
        public IReadOnlyList<string>? Fragments { get; set; }

        /// <summary>Gets or sets the progression within the resource (0..1).</summary>
        [JsonPropertyName("progression")]
        public double? Progression { get; set; }

        /// <summary>Gets or sets the progression within the whole book (0..1).</summary>
        [JsonPropertyName("totalProgression")]
        public double? TotalProgression { get; set; }
    }
}
