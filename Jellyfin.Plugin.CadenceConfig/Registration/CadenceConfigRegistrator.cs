using System;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CadenceConfig.Registration
{
    /// <summary>
    /// Registers CadenceConfig services into Jellyfin's DI container at startup — the named HTTP
    /// client the Lidarr proxy uses to reach Lidarr.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class CadenceConfigRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddHttpClient("CadenceConfig", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin-CadenceConfig/1.0 (+https://github.com/johnpc/jellyfin-plugin-cadence-config)");
            });

            // The Deezer import controller resolves this to read public playlists.
            serviceCollection.AddSingleton<DeezerClient>();
        }
    }
}
