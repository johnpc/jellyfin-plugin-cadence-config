using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>A Deezer track's album — title + cover art URL, used to tag a grabbed file so it
    /// lands in the library with clean metadata (a YouTube grab has none).</summary>
    public sealed class DeezerAlbum
    {
        /// <summary>Gets or sets the album title.</summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>Gets or sets the largest cover-art URL (1000×1000).</summary>
        [JsonPropertyName("cover_xl")]
        public string? CoverXl { get; set; }
    }
}
