using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Covers
{
    /// <summary>
    /// Generates a mosaic Primary image for playlists that have none, so every client (not just
    /// Cadence) shows a real cover instead of a placeholder. For each art-less playlist it picks up to
    /// four distinct track/album images (<see cref="CoverSelection"/>), draws a 2×2 mosaic
    /// (<see cref="MosaicRenderer"/>), and saves it via Jellyfin's provider manager. NEVER touches a
    /// playlist that already has a Primary image (user-set art is preserved). Thin I/O plumbing around
    /// the pure selection + render helpers, so it's excluded from coverage.
    /// </summary>
    public sealed class PlaylistCoverService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;
        private readonly ILogger<PlaylistCoverService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistCoverService"/> class.
        /// </summary>
        /// <param name="libraryManager">Jellyfin library manager (enumerate playlists + resolve albums).</param>
        /// <param name="providerManager">Jellyfin provider manager (save the generated image).</param>
        /// <param name="logger">The logger.</param>
        public PlaylistCoverService(
            ILibraryManager libraryManager,
            IProviderManager providerManager,
            ILogger<PlaylistCoverService> logger)
        {
            _libraryManager = libraryManager;
            _providerManager = providerManager;
            _logger = logger;
        }

        /// <summary>
        /// Generate covers for every art-less playlist, reporting 0..100 progress. Returns the number
        /// of covers written. Never throws for a single playlist — logs and continues.
        /// </summary>
        /// <param name="progress">Progress reporter (0..100).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The count of covers generated.</returns>
        public async Task<int> GenerateAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var playlists = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Playlist },
                Recursive = true,
            });

            var generated = 0;
            for (var i = 0; i < playlists.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (playlists[i] is Playlist playlist && !playlist.HasImage(ImageType.Primary, 0))
                {
                    if (await TryGenerateAsync(playlist, cancellationToken).ConfigureAwait(false))
                    {
                        generated++;
                    }
                }

                progress.Report((i + 1) * 100.0 / Math.Max(1, playlists.Count));
            }

            _logger.LogInformation("Playlist covers: generated {Count} cover(s).", generated);
            return generated;
        }

        private async Task<bool> TryGenerateAsync(Playlist playlist, CancellationToken cancellationToken)
        {
            try
            {
                var png = await BuildCoverAsync(playlist, cancellationToken).ConfigureAwait(false);
                if (png is null)
                {
                    return false;
                }

                using var stream = new MemoryStream(png);
                await _providerManager
                    .SaveImage(playlist, stream, "image/png", ImageType.Primary, null, cancellationToken)
                    .ConfigureAwait(false);
                await playlist.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Playlist covers: set cover for {Name}.", playlist.Name);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Playlist covers: failed for {Name}.", playlist.Name);
                return false;
            }
        }

        /// <summary>A track's primary image path — its own if present, else its owning MusicAlbum's,
        /// else null (so it's skipped from the mosaic).</summary>
        private string? PrimaryOrAlbumImagePath(BaseItem track)
        {
            if (track.HasImage(ImageType.Primary, 0))
            {
                return track.GetImagePath(ImageType.Primary, 0);
            }

            var album = (track as Audio)?.FindParent<MusicAlbum>();
            return album is not null && album.HasImage(ImageType.Primary, 0)
                ? album.GetImagePath(ImageType.Primary, 0)
                : null;
        }

        /// <summary>Build the cover PNG for a playlist: a mosaic from its tracks' album art when there's
        /// enough, else a name-based tile (so empty playlists — which Jellyfin leaves uncovered — still
        /// get art). Returns null only when even the name cover can't render (blank name).</summary>
        private async Task<byte[]?> BuildCoverAsync(Playlist playlist, CancellationToken cancellationToken)
        {
            var tracks = playlist.GetLinkedChildren();
            var trackImagePaths = new List<string?>(tracks.Count);
            foreach (var track in tracks)
            {
                trackImagePaths.Add(PrimaryOrAlbumImagePath(track));
            }

            var tilePaths = CoverSelection.PickTiles(trackImagePaths);
            if (tilePaths.Count > 0)
            {
                var sources = new List<byte[]>(tilePaths.Count);
                foreach (var path in tilePaths)
                {
                    sources.Add(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false));
                }

                var mosaic = MosaicRenderer.Render(sources);
                if (mosaic is not null)
                {
                    return mosaic;
                }
            }

            // No usable track art (empty playlist, or tracks without covers) → name tile.
            return NameCoverRenderer.Render(playlist.Name);
        }
    }
}
