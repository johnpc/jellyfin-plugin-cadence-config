using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>A page of Deezer tracks plus the URL of the next page (paged 100 at a time).</summary>
    public sealed class DeezerTrackList
    {
        /// <summary>Gets or sets this page's tracks.</summary>
        [JsonPropertyName("data")]
        public IReadOnlyList<DeezerTrack> Data { get; set; } = new List<DeezerTrack>();

        /// <summary>Gets or sets the absolute URL of the next page, or null on the last page.</summary>
        [JsonPropertyName("next")]
        public string? Next { get; set; }
    }
}
