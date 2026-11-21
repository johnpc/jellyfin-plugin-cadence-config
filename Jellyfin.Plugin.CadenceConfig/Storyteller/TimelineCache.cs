using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Keeps built timelines: in memory for the process, and as JSON on disk so a Jellyfin restart
    /// doesn't re-probe dozens of audio chunks per book. Keyed by book uuid + Storyteller's
    /// aligned/updated fingerprint, so a re-alignment invalidates automatically.
    /// </summary>
    public sealed class TimelineCache
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly ConcurrentDictionary<string, Timeline> _memory = new();
        private readonly string _directory;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimelineCache"/> class.
        /// </summary>
        /// <param name="directory">The on-disk cache directory (created on first write).</param>
        public TimelineCache(string directory)
        {
            _directory = directory;
        }

        /// <summary>A filesystem-safe file name for a (uuid, fingerprint) pair.</summary>
        /// <param name="bookUuid">The book.</param>
        /// <param name="fingerprint">The fingerprint.</param>
        /// <returns>The file name.</returns>
        public static string FileName(string bookUuid, string fingerprint)
        {
            var safe = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fingerprint)))[..16];
            return $"{bookUuid}-{safe}.json";
        }

        /// <summary>Returns the cached timeline for the pair, or null.</summary>
        /// <param name="bookUuid">The book.</param>
        /// <param name="fingerprint">The fingerprint.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The timeline, or null.</returns>
        public async Task<Timeline?> GetAsync(string bookUuid, string fingerprint, CancellationToken cancellationToken)
        {
            var name = FileName(bookUuid, fingerprint);
            if (_memory.TryGetValue(name, out var hit))
            {
                return hit;
            }

            var path = Path.Combine(_directory, name);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                using var stream = File.OpenRead(path);
                var loaded = await JsonSerializer.DeserializeAsync<Timeline>(stream, Json, cancellationToken).ConfigureAwait(false);
                if (loaded != null)
                {
                    _memory[name] = loaded;
                }

                return loaded;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>Stores a timeline in memory and on disk (best effort).</summary>
        /// <param name="timeline">The timeline (its BookUuid + Fingerprint are the key).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task.</returns>
        public async Task PutAsync(Timeline timeline, CancellationToken cancellationToken)
        {
            var name = FileName(timeline.BookUuid, timeline.Fingerprint);
            _memory[name] = timeline;
            try
            {
                Directory.CreateDirectory(_directory);
                using var stream = File.Create(Path.Combine(_directory, name));
                await JsonSerializer.SerializeAsync(stream, timeline, Json, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Disk cache is an optimization only; memory still holds it.
            }
        }
    }
}
