using System;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CadenceConfig.Chapters;
using Jellyfin.Plugin.CadenceConfig.Covers;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using Jellyfin.Plugin.CadenceConfig.Grab;
using Jellyfin.Plugin.CadenceConfig.Home;
using Jellyfin.Plugin.CadenceConfig.Sync;
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

            // Shared by the import controller (one-shot) and the sync scheduled task (recurring).
            serviceCollection.AddSingleton<DeezerImportService>();

            // Extracts embedded audiobook (m4b) chapters Jellyfin doesn't surface for audio.
            serviceCollection.AddSingleton<ChapterService>();

            // Generates mosaic/name covers for art-less playlists (PlaylistCoverTask runs it).
            serviceCollection.AddSingleton<PlaylistCoverService>();

            // Music Grabber: fetch Deezer-missing tracks server-side during import (the grabber tags
            // + files them from the metadata we pass, so no tagging is needed on our side).
            serviceCollection.AddSingleton<MusicGrabberClient>();
            serviceCollection.AddSingleton<GrabFulfillmentService>();

            // Precomputed Home shelves: the cache is shared between the daily task, the controller
            // (reader), and the refresher (background writer). The refresher rebuilds off the request
            // thread (stale-while-revalidate) so no user ever waits on a recursive-scan compute.
            serviceCollection.AddSingleton<HomeShelvesCache>();
            serviceCollection.AddSingleton<IHomeShelvesService, HomeShelvesService>();
            serviceCollection.AddSingleton<HomeShelvesRefresher>();
        }
    }
}
