using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Grab
{
    /// <summary>The /api/search response: results + a token echoed back on download.</summary>
    public sealed class GrabSearchResponse
    {
        /// <summary>Gets or sets the candidate results.</summary>
        [JsonPropertyName("results")]
        public IReadOnlyList<GrabResult> Results { get; set; } = new List<GrabResult>();

        /// <summary>Gets or sets the search token to pass to /api/download.</summary>
        [JsonPropertyName("search_token")]
        public string? SearchToken { get; set; }
    }
}
