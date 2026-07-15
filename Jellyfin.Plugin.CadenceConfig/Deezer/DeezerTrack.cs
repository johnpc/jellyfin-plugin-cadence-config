using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>A single Deezer track — just the fields needed to match against Jellyfin.</summary>
    public sealed class DeezerTrack
    {
        /// <summary>Gets or sets the track title.</summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>Gets or sets the track's artist.</summary>
        [JsonPropertyName("artist")]
        public DeezerArtist? Artist { get; set; }
    }
}
