using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>A Deezer artist reference (name only).</summary>
    public sealed class DeezerArtist
    {
        /// <summary>Gets or sets the artist name.</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
