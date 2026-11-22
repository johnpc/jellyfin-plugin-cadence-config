using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>An author/narrator entry.</summary>
    public sealed class StorytellerCreator
    {
        /// <summary>Gets or sets the display name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
