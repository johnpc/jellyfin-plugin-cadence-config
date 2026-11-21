using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Produces (and caches) the <see cref="Timeline"/> of an aligned Storyteller book: manifest →
    /// one guided-navigation document per chapter → probe every audio chunk's duration → flatten.
    /// The heavy work happens once per (book, alignment) thanks to <see cref="TimelineCache"/>.
    /// </summary>
    public sealed class TimelineBuilder
    {
        private readonly StorytellerClient _client;
        private readonly TimelineCache _cache;
        private readonly ILogger<TimelineBuilder> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimelineBuilder"/> class.
        /// </summary>
        /// <param name="client">The Storyteller client.</param>
        /// <param name="cache">The timeline cache.</param>
        /// <param name="logger">The logger.</param>
        public TimelineBuilder(StorytellerClient client, TimelineCache cache, ILogger<TimelineBuilder> logger)
        {
            _client = client;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>Every audio chunk referenced by the guides (root-relative), in natural order.</summary>
        /// <param name="manifest">The manifest.</param>
        /// <param name="guides">The guides by chapter href.</param>
        /// <returns>The chunk paths.</returns>
        public static IReadOnlyList<string> AudioFiles(ReadiumManifest manifest, IReadOnlyDictionary<string, GuidedNavigationDocument> guides)
        {
            var files = new HashSet<string>();
            foreach (var chapter in manifest.ReadingOrder)
            {
                var href = chapter.Href.TrimStart('/');
                if (!guides.TryGetValue(href, out var guide))
                {
                    continue;
                }

                foreach (var node in OverlayTimeline.Flatten(guide.Guided))
                {
                    var clip = Audioref.Parse(node.Audioref);
                    if (clip != null)
                    {
                        files.Add(StorytellerUrls.ResolveAgainst(href, clip.Path));
                    }
                }
            }

            return files.OrderBy(f => f, NaturalOrder.Instance).ToList();
        }

        /// <summary>Returns the book's timeline, building it when not cached; null when Storyteller can't serve it.</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <param name="token">The user's token.</param>
        /// <param name="book">The aligned Storyteller book.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The timeline, or null.</returns>
        public async Task<Timeline?> GetAsync(string baseUrl, string token, StorytellerBook book, CancellationToken cancellationToken)
        {
            var cached = await _cache.GetAsync(book.Uuid, book.Fingerprint, cancellationToken).ConfigureAwait(false);
            if (cached != null)
            {
                return cached;
            }

            var manifest = await _client.GetManifestAsync(baseUrl, token, book.Uuid, cancellationToken).ConfigureAwait(false);
            if (manifest == null)
            {
                return null;
            }

            var guides = new Dictionary<string, GuidedNavigationDocument>();
            foreach (var chapter in manifest.ReadingOrder)
            {
                var href = chapter.Href.TrimStart('/');
                var guide = await _client.GetGuidedNavigationAsync(baseUrl, token, book.Uuid, href, cancellationToken).ConfigureAwait(false);
                if (guide != null)
                {
                    guides[href] = guide;
                }
            }

            var files = AudioFiles(manifest, guides);
            var durations = new Dictionary<string, double>();
            foreach (var file in files)
            {
                var seconds = await ProbeAsync(baseUrl, token, book.Uuid, file, cancellationToken).ConfigureAwait(false);
                if (seconds != null)
                {
                    durations[file] = seconds.Value;
                }
                else
                {
                    _logger.LogWarning("CadenceConfig: could not read the duration of {File} in {Book}; using its last clip end.", file, book.Uuid);
                }
            }

            var timeline = OverlayTimeline.Build(manifest, guides, durations);
            timeline.BookUuid = book.Uuid;
            timeline.Fingerprint = book.Fingerprint;
            await _cache.PutAsync(timeline, cancellationToken).ConfigureAwait(false);
            return timeline;
        }

        private Task<double?> ProbeAsync(string baseUrl, string token, string bookUuid, string file, CancellationToken cancellationToken)
        {
            return Mp4Duration.ReadAsync(
                (offset, length) => _client.ReadRangeAsync(baseUrl, token, bookUuid, file, offset, length, cancellationToken),
                null);
        }
    }
}
