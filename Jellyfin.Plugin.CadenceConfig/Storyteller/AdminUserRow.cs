using System;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>One Jellyfin user as the dashboard lists them. Carries no secret.</summary>
    public sealed class AdminUserRow
    {
        /// <summary>Gets or sets the Jellyfin user id.</summary>
        public Guid JellyfinUserId { get; set; }

        /// <summary>Gets or sets the Jellyfin username.</summary>
        public string JellyfinUsername { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the user has a Storyteller link.</summary>
        public bool Linked { get; set; }

        /// <summary>Gets or sets the Storyteller username (null when unlinked).</summary>
        public string? StorytellerUsername { get; set; }

        /// <summary>Gets or sets "mapped" / "provisioned" (null when unlinked).</summary>
        public string? Kind { get; set; }
    }
}
