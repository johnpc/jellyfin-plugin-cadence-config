using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>Readium Guided Navigation document — one per chapter.</summary>
    public sealed class GuidedNavigationDocument
    {
        /// <summary>Gets or sets the guided nodes.</summary>
        [JsonPropertyName("guided")]
        public IReadOnlyList<GuidedNode> Guided { get; set; } = Array.Empty<GuidedNode>();
    }
}
