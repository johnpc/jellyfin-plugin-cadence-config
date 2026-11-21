using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The body of Storyteller's <c>POST /api/v2/token</c> reply.</summary>
    public sealed class StorytellerToken
    {
        /// <summary>Gets or sets the bearer token.</summary>
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }
}
