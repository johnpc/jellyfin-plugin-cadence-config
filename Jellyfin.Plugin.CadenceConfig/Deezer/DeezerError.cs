using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>A Deezer API error envelope (present when a request is rejected).</summary>
    public sealed class DeezerError
    {
        /// <summary>Gets or sets the error type.</summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>Gets or sets the human-readable message.</summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
