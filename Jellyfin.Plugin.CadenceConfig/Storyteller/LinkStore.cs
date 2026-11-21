using System;
using System.Linq;
using Jellyfin.Plugin.CadenceConfig.Configuration;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Pure operations over the persisted <see cref="StorytellerLink"/> array in plugin configuration:
    /// find, upsert, remove. Each mutation returns a NEW array (the caller saves the configuration).
    /// </summary>
    public static class LinkStore
    {
        /// <summary>Resolves the link state for a user.</summary>
        /// <param name="config">The plugin configuration.</param>
        /// <param name="userId">The Jellyfin user.</param>
        /// <returns>The state.</returns>
        public static LinkState StateFor(PluginConfiguration config, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(config.StorytellerUrl))
            {
                return LinkState.Unconfigured;
            }

            return Find(config, userId) == null ? LinkState.Unlinked : LinkState.Linked;
        }

        /// <summary>Finds a user's link, or null.</summary>
        /// <param name="config">The plugin configuration.</param>
        /// <param name="userId">The Jellyfin user.</param>
        /// <returns>The link with a non-empty token, or null.</returns>
        public static StorytellerLink? Find(PluginConfiguration config, Guid userId)
        {
            return (config.StorytellerLinks ?? Array.Empty<StorytellerLink>())
                .FirstOrDefault(l => l.JellyfinUserId == userId && !string.IsNullOrEmpty(l.Token));
        }

        /// <summary>Returns the links with the given user's link added or replaced.</summary>
        /// <param name="links">The current links.</param>
        /// <param name="link">The link to store.</param>
        /// <returns>A new array.</returns>
        public static StorytellerLink[] Upsert(StorytellerLink[]? links, StorytellerLink link)
        {
            var kept = (links ?? Array.Empty<StorytellerLink>()).Where(l => l.JellyfinUserId != link.JellyfinUserId);
            return kept.Append(link).ToArray();
        }

        /// <summary>Returns the links without the given user's link.</summary>
        /// <param name="links">The current links.</param>
        /// <param name="userId">The user to unlink.</param>
        /// <returns>A new array.</returns>
        public static StorytellerLink[] Remove(StorytellerLink[]? links, Guid userId)
        {
            return (links ?? Array.Empty<StorytellerLink>()).Where(l => l.JellyfinUserId != userId).ToArray();
        }
    }
}
