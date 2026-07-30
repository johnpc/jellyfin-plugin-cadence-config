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

        /// <summary>Gets or sets the track's album (title + cover art), for tagging a grabbed file.</summary>
        [JsonPropertyName("album")]
        public DeezerAlbum? Album { get; set; }

        /// <summary>Gets or sets the track's position within its album (1-based), for the track-number tag.</summary>
        [JsonPropertyName("track_position")]
        public int TrackPosition { get; set; }
    }
}
