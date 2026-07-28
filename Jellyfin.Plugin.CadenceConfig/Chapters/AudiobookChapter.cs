using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Chapters
{
    /// <summary>
    /// A single audiobook chapter, as served to the Cadence client. Position is in
    /// SECONDS (not Jellyfin's 100-ns ticks) so the web/native player can seek with
    /// `audio.currentTime = start` directly, with no unit conversion on the client.
    /// </summary>
    public sealed class AudiobookChapter
    {
        /// <summary>Gets or sets the chapter title (falls back to "Chapter N" when the file has none).</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the chapter start, in seconds from the file start.</summary>
        [JsonPropertyName("start")]
        public double Start { get; set; }
    }
}
