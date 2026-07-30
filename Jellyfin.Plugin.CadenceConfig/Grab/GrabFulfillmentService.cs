using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Deezer;
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
        // Serial: the grabber returns 0/errors when searches arrive concurrently (verified live), and
        // a background playlist backfill isn't time-critical. One track at a time is reliable.
        private const int MaxConcurrent = 1;

        private readonly MusicGrabberClient _grabber;
        private readonly ILogger<GrabFulfillmentService> _logger;

        /// <summary>Initializes a new instance of the <see cref="GrabFulfillmentService"/> class.</summary>
        /// <param name="grabber">The Music Grabber client.</param>
        /// <param name="logger">The logger.</param>
        public GrabFulfillmentService(
            MusicGrabberClient grabber,
            ILogger<GrabFulfillmentService> logger)
        {
            _grabber = grabber;
            _logger = logger;
        }

        /// <summary>
        /// Grab each missing track via Music Grabber (no-op when unconfigured), bounded-concurrent.
        /// The grabber tags + files each track from the metadata we pass and rescans the library
        /// itself, so there's nothing to tag or scan here. Returns how many downloaded; grabber misses
        /// fall back to the Lidarr per-artist request (still in the import's MissingArtists).
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
                if (string.IsNullOrWhiteSpace(track.Title))
                {
                    continue;
                }

                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                tasks.Add(Task.Run(
                    async () =>
                    {
                        try
                        {
                            if (await _grabber.GrabAsync(track, cancellationToken).ConfigureAwait(false))
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
            _logger.LogInformation("Grab fulfillment: {Count} of {Total} missing track(s) downloaded.", grabbed, missing.Count);
            return grabbed;
        }
    }
}
