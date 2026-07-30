using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Grab
{
    /// <summary>One search result from Music Grabber (a candidate download for a query).</summary>
    public sealed class GrabResult
    {
        /// <summary>Gets or sets the source-specific id (e.g. YouTube video id).</summary>
        [JsonPropertyName("video_id")]
        public string? VideoId { get; set; }

        /// <summary>Gets or sets the result title (often noisy, e.g. "Artist - Title (Official Audio)").</summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>Gets or sets the result's artist, if the source provides one.</summary>
        [JsonPropertyName("artist")]
        public string? Artist { get; set; }

        /// <summary>Gets or sets the uploading channel (YouTube), used when artist is null.</summary>
        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        /// <summary>Gets or sets the source name (youtube, soulseek, …).</summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        /// <summary>Gets or sets the source URL.</summary>
        [JsonPropertyName("source_url")]
        public string? SourceUrl { get; set; }

        /// <summary>Gets or sets a source ranking score (higher is better).</summary>
        [JsonPropertyName("quality_score")]
        public int QualityScore { get; set; }

        /// <summary>Gets or sets the Soulseek username (only for slskd results).</summary>
        [JsonPropertyName("slskd_username")]
        public string? SlskdUsername { get; set; }

        /// <summary>Gets or sets the Soulseek filename (only for slskd results).</summary>
        [JsonPropertyName("slskd_filename")]
        public string? SlskdFilename { get; set; }

        /// <summary>Gets or sets the Soulseek file size (only for slskd results).</summary>
        [JsonPropertyName("slskd_size")]
        public long? SlskdSize { get; set; }
    }
}
