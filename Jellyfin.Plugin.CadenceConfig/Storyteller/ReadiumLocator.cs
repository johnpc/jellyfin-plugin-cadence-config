using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>A Readium locator — Storyteller's reading-position unit.</summary>
    public sealed class ReadiumLocator
    {
        /// <summary>Gets or sets the resource href (chapter XHTML or audio file).</summary>
        [JsonPropertyName("href")]
        public string Href { get; set; } = string.Empty;

        /// <summary>Gets or sets the media type of the resource.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>Gets or sets the chapter title.</summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>Gets or sets the locations.</summary>
        [JsonPropertyName("locations")]
        public ReadiumLocations? Locations { get; set; }
    }
}
