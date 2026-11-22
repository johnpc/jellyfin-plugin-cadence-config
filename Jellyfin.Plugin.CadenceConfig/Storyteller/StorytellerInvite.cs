using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>Storyteller's answer to creating an invite: the email and the one-time key.</summary>
    public sealed class StorytellerInvite
    {
        /// <summary>Gets or sets the invited email.</summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        /// <summary>Gets or sets the one-time invite key.</summary>
        [JsonPropertyName("key")]
        public string? Key { get; set; }
    }
}
