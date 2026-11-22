using System;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CadenceConfig.Configuration;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The production <see cref="ILinkWriter"/>: the plugin's own configuration.</summary>
    [ExcludeFromCodeCoverage]
    public sealed class PluginLinkWriter : ILinkWriter
    {
        /// <inheritdoc />
        public PluginConfiguration Current() => Plugin.GetConfiguration();

        /// <inheritdoc />
        public void Upsert(StorytellerLink link)
        {
            var config = Plugin.GetConfiguration();
            config.StorytellerLinks = LinkStore.Upsert(config.StorytellerLinks, link);
            Save();
        }

        /// <inheritdoc />
        public void Remove(Guid jellyfinUserId)
        {
            var config = Plugin.GetConfiguration();
            config.StorytellerLinks = LinkStore.Remove(config.StorytellerLinks, jellyfinUserId);
            Save();
        }

        /// <inheritdoc />
        public void Save() => Plugin.Instance?.SaveConfiguration();
    }
}
