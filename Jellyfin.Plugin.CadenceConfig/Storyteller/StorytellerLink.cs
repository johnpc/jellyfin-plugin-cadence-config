using System;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// A persisted link between one Jellyfin user and their Storyteller account: the session token
    /// the plugin uses, on that user's behalf, to read the aligned book and sync positions. Stored in
    /// plugin configuration. The token NEVER leaves the server — clients only ever see
    /// <see cref="Username"/> and a linked/unlinked flag.
    /// </summary>
    public class StorytellerLink
    {
        /// <summary>Gets or sets the Jellyfin user this link belongs to.</summary>
        public Guid JellyfinUserId { get; set; }

        /// <summary>Gets or sets the Storyteller username (display only).</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Gets or sets the Storyteller bearer token (server-side secret).</summary>
        public string Token { get; set; } = string.Empty;
    }
}
