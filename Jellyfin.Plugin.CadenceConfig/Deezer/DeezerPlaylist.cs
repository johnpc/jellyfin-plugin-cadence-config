using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>
    /// The subset of Deezer's public playlist API (api.deezer.com/playlist/{id}) Cadence consumes.
    /// Public playlists need NO auth; fields not listed are ignored by the deserializer.
    /// </summary>
    public sealed class DeezerPlaylist
    {
        /// <summary>Gets or sets the playlist title.</summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>Gets or sets the total track count (the playlist may be paged).</summary>
        [JsonPropertyName("nb_tracks")]
        public int NbTracks { get; set; }

        /// <summary>Gets or sets the (first page of) tracks.</summary>
        [JsonPropertyName("tracks")]
        public DeezerTrackList? Tracks { get; set; }

        /// <summary>Gets or sets an API error, when the request failed (e.g. private/not found).</summary>
        [JsonPropertyName("error")]
        public DeezerError? Error { get; set; }
    }
}
