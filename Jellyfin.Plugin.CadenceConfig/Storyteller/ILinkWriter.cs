using Jellyfin.Plugin.CadenceConfig.Configuration;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Where links live. The plugin configuration in production; an in-memory stand-in in tests, so
    /// the provisioner is testable without a running Jellyfin.
    /// </summary>
    public interface ILinkWriter
    {
        /// <summary>The current configuration (links, URL, admin token).</summary>
        /// <returns>The configuration.</returns>
        PluginConfiguration Current();

        /// <summary>Adds or replaces the link for its Jellyfin user and persists.</summary>
        /// <param name="link">The link.</param>
        void Upsert(StorytellerLink link);

        /// <summary>Removes a user's link and persists.</summary>
        /// <param name="jellyfinUserId">The Jellyfin user.</param>
        void Remove(System.Guid jellyfinUserId);

        /// <summary>Persists the current configuration after fields were edited in place (admin login).</summary>
        void Save();
    }
}
