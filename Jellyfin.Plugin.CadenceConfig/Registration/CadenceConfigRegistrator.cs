using System;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CadenceConfig.Audiobooks;
using Jellyfin.Plugin.CadenceConfig.Chapters;
using Jellyfin.Plugin.CadenceConfig.Covers;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using Jellyfin.Plugin.CadenceConfig.Sync;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

            // Caches the whole audiobook library as client-ready DTOs so the Cadence client fetches it
            // in one fast call (no per-request recursive scan). The hosted invalidator drops the cache
            // on library changes; the refresh scheduled task warms it on startup + daily.
            serviceCollection.AddSingleton<AudiobookLibraryService>();
            serviceCollection.AddHostedService<AudiobookLibraryInvalidator>();

            // Generates mosaic/name covers for art-less playlists (PlaylistCoverTask runs it).
            serviceCollection.AddSingleton<PlaylistCoverService>();
        }
    }
}
