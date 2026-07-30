using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Grab
{
    /// <summary>
    /// Fetches the tracks a Deezer import couldn't find in the library: for each, grab the single song
    /// via Music Grabber and stamp clean tags from the Deezer metadata (title/artist/album/#/cover),
    /// so it lands grouped correctly instead of as a junk-tagged loose file. Grabber misses are left
    /// for the Lidarr per-artist fallback (still surfaced as MissingArtists by the import). After
    /// grabbing, queues a library scan so the new files register; the Deezer sync task then folds them
    /// into the playlist. Bounded concurrency so a big playlist doesn't hammer the grabber.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class GrabFulfillmentService
    {
        private const int MaxConcurrent = 3;

        private readonly MusicGrabberClient _grabber;
        private readonly TrackTagger _tagger;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<GrabFulfillmentService> _logger;

        /// <summary>Initializes a new instance of the <see cref="GrabFulfillmentService"/> class.</summary>
        /// <param name="grabber">The Music Grabber client.</param>
        /// <param name="tagger">The ffmpeg track tagger.</param>
        /// <param name="libraryManager">Jellyfin library manager (to queue a rescan).</param>
        /// <param name="logger">The logger.</param>
        public GrabFulfillmentService(
            MusicGrabberClient grabber,
            TrackTagger tagger,
            ILibraryManager libraryManager,
            ILogger<GrabFulfillmentService> logger)
        {
            _grabber = grabber;
            _tagger = tagger;
            _libraryManager = libraryManager;
            _logger = logger;
        }

        /// <summary>
        /// Grab + tag each missing track (no-op when the grabber isn't configured). Returns how many
        /// were successfully downloaded. Queues a library scan at the end if anything landed.
        /// </summary>
        /// <param name="missing">The Deezer tracks not found in the library.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of tracks grabbed.</returns>
        public async Task<int> FulfillAsync(IReadOnlyList<DeezerTrack> missing, CancellationToken cancellationToken)
        {
            if (missing.Count == 0 || !_grabber.IsConfigured())
            {
                return 0;
            }

            var grabbed = 0;
            using var gate = new SemaphoreSlim(MaxConcurrent);
            var tasks = new List<Task>(missing.Count);
            foreach (var track in missing)
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                tasks.Add(Task.Run(
                    async () =>
                    {
                        try
                        {
                            if (await GrabOneAsync(track, cancellationToken).ConfigureAwait(false))
                            {
                                Interlocked.Increment(ref grabbed);
                            }
                        }
                        finally
                        {
                            gate.Release();
                        }
                    },
                    cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            if (grabbed > 0)
            {
                _logger.LogInformation("Grab fulfillment: {Count} of {Total} missing track(s) downloaded; queuing scan.", grabbed, missing.Count);
                _libraryManager.QueueLibraryScan();
            }

            return grabbed;
        }

        private async Task<bool> GrabOneAsync(DeezerTrack track, CancellationToken cancellationToken)
        {
            var title = track.Title ?? string.Empty;
            var artist = track.Artist?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            var path = await _grabber.GrabAsync(title, artist, cancellationToken).ConfigureAwait(false);
            if (path is null)
            {
                return false; // grabber miss → Lidarr fallback (still in MissingArtists)
            }

            await _tagger
                .TagAsync(path, title, artist, track.Album?.Title, track.TrackPosition, track.Album?.CoverXl, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
    }
}
