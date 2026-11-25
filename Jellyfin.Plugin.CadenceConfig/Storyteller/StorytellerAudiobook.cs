using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The audiobook side of a Storyteller book (the fields we read).</summary>
    public sealed class StorytellerAudiobook
    {
        /// <summary>Gets or sets the audiobook directory inside Storyteller, e.g. <c>/library/Andy Weir - Project Hail Mary</c>.</summary>
        [JsonPropertyName("filepath")]
        public string? Filepath { get; set; }
    }
}
