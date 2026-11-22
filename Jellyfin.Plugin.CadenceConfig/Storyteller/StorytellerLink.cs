using System;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// One Jellyfin user's Storyteller identity, persisted in the plugin configuration (server-side
    /// only). The token is what the bridge acts with; it is never returned by any endpoint.
    /// </summary>
    public class StorytellerLink
    {
        /// <summary>Gets or sets the Jellyfin user this link belongs to.</summary>
        public Guid JellyfinUserId { get; set; }

        /// <summary>Gets or sets the Storyteller username (shown to the admin; never secret).</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Gets or sets the Storyteller bearer token. Server-side secret.</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>Gets or sets how the link came to be (mapped by the admin, or provisioned by the plugin).</summary>
        public LinkKind Kind { get; set; } = LinkKind.Mapped;

        /// <summary>
        /// Gets or sets the generated password — ONLY for <see cref="LinkKind.Provisioned"/> accounts, so
        /// the admin can hand the person their Storyteller login. Blank for mapped accounts. Server-side
        /// secret: revealed only through the admin-only credentials endpoint.
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }
}
