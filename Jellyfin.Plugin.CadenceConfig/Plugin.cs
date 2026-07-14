using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CadenceConfig.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.CadenceConfig
{
    /// <summary>
    /// The main plugin class for CadenceConfig. Serves the Cadence music client its runtime
    /// configuration (marlin/signup/cast URLs) and proxies Lidarr "request music" calls with the
    /// API key injected server-side, so every client — web and native — is auto-configured and no
    /// secret ever leaves the server.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <inheritdoc />
        public override string Name => "CadenceConfig";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("7d49437c-2e97-430f-b6b7-fa9d58110448");

        /// <inheritdoc />
        public override string Description =>
            "Serves the Cadence music client its runtime config (search/signup/cast URLs) and proxies Lidarr requests with the API key kept server-side.";

        /// <summary>
        /// Gets the current plugin configuration, or defaults if the plugin is not loaded.
        /// </summary>
        /// <returns>The active configuration.</returns>
        public static PluginConfiguration GetConfiguration()
        {
            return Instance?.Configuration ?? new PluginConfiguration();
        }

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html",
                },
            };
        }
    }
}
