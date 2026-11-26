using System;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CadenceConfig.Covers;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using Jellyfin.Plugin.CadenceConfig.Grab;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Jellyfin.Plugin.CadenceConfig.Sync;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.CadenceConfig.Registration
{
    /// <summary>
    /// Registers CadenceConfig services into Jellyfin's DI container at startup — the named HTTP
    /// client the Lidarr proxy uses to reach Lidarr, the Deezer/grab services, and the Storyteller bridge.
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

            // Generates mosaic/name covers for art-less playlists (PlaylistCoverTask runs it).
            serviceCollection.AddSingleton<PlaylistCoverService>();

            // Music Grabber: fetch Deezer-missing tracks server-side during import (the grabber tags
            // + files them from the metadata we pass, so no tagging is needed on our side).
            serviceCollection.AddSingleton<MusicGrabberClient>();
            serviceCollection.AddSingleton<GrabFulfillmentService>();

            // Storyteller (Whispersync + read-along): the API client, the per-user book catalog, the
            // timeline builder with its on-disk cache, Jellyfin plumbing, and the two bridge halves —
            // PositionBridge pushes saved positions as they happen; StorytellerSyncTask pulls on a timer.
            serviceCollection.AddSingleton<StorytellerClient>();
            serviceCollection.AddSingleton<StorytellerAccounts>();
            serviceCollection.AddSingleton<ILinkWriter, PluginLinkWriter>();
            serviceCollection.AddSingleton<UserProvisioner>();
            serviceCollection.AddSingleton<BookCatalog>();
            serviceCollection.AddSingleton(sp => new TimelineCache(
                System.IO.Path.Combine(sp.GetRequiredService<IApplicationPaths>().CachePath, "cadence-storyteller")));
            serviceCollection.AddSingleton<TimelineBuilder>();
            serviceCollection.AddSingleton<JellyfinBooks>();
            serviceCollection.AddSingleton<ReadAlongService>();
            serviceCollection.AddSingleton<LibraryResponseCache>();
            serviceCollection.AddSingleton<ReadAlongLibraryService>();
            serviceCollection.AddSingleton<PositionService>();
            serviceCollection.AddSingleton<IHostedService, PositionBridge>();
        }
    }
}
