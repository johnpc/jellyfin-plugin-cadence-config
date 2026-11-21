using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>A guided-navigation node: a text fragment tied to an audio clip, possibly nested.</summary>
    public sealed class GuidedNode
    {
        /// <summary>Gets or sets the text reference (<c>chapter.xhtml#id</c>).</summary>
        [JsonPropertyName("textref")]
        public string? Textref { get; set; }

        /// <summary>Gets or sets the audio reference (<c>audio/x.mp4#t=1.0,2.5</c>).</summary>
        [JsonPropertyName("audioref")]
        public string? Audioref { get; set; }

        /// <summary>Gets or sets the nested nodes.</summary>
        [JsonPropertyName("children")]
        public IReadOnlyList<GuidedNode>? Children { get; set; }
    }
}
