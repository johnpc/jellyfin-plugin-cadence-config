using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Grab
{
    /// <summary>
    /// Rewrites a grabbed file's tags (title / artist / album / track# / cover art) from the clean
    /// Deezer metadata, using Jellyfin's own ffmpeg. A YouTube grab lands with a noisy title
    /// ("Artist - Title (Official Audio)"), no artist/album, and no art — which would scatter it into
    /// junk albums in the library. Stamping the real values makes Jellyfin group it correctly with a
    /// cover. Runs ffmpeg to a temp file then atomically replaces the original (ffmpeg can't edit in
    /// place). Best-effort — a tagging failure leaves the grabbed file untouched.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class TrackTagger
    {
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TrackTagger> _logger;

        /// <summary>Initializes a new instance of the <see cref="TrackTagger"/> class.</summary>
        /// <param name="mediaEncoder">Jellyfin's media encoder (for the ffmpeg binary path).</param>
        /// <param name="httpClientFactory">HTTP factory (to fetch the cover image).</param>
        /// <param name="logger">The logger.</param>
        public TrackTagger(
            IMediaEncoder mediaEncoder,
            IHttpClientFactory httpClientFactory,
            ILogger<TrackTagger> logger)
        {
            _mediaEncoder = mediaEncoder;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Tag <paramref name="filePath"/> in place from the given metadata. Returns true on success.
        /// </summary>
        /// <param name="filePath">The grabbed audio file (must exist).</param>
        /// <param name="title">Clean track title.</param>
        /// <param name="artist">Clean artist name.</param>
        /// <param name="album">Album title (may be null).</param>
        /// <param name="trackNumber">1-based track position (0 = omit).</param>
        /// <param name="coverUrl">Cover-art URL to embed (may be null).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True when the file was retagged.</returns>
        public async Task<bool> TagAsync(
            string filePath,
            string title,
            string artist,
            string? album,
            int trackNumber,
            string? coverUrl,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            var dir = Path.GetDirectoryName(filePath) ?? Path.GetTempPath();
            var ext = Path.GetExtension(filePath);
            var tmp = Path.Combine(dir, $".cadence-tag-{Guid.NewGuid():N}{ext}");
            var coverPath = await DownloadCoverAsync(coverUrl, dir, cancellationToken).ConfigureAwait(false);
            try
            {
                var ok = await RunFfmpegAsync(filePath, tmp, coverPath, title, artist, album, trackNumber, cancellationToken)
                    .ConfigureAwait(false);
                if (ok && File.Exists(tmp))
                {
                    File.Move(tmp, filePath, overwrite: true);
                    return true;
                }

                return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "TrackTagger: failed to tag {Path}.", filePath);
                return false;
            }
            finally
            {
                TryDelete(tmp);
                TryDelete(coverPath);
            }
        }

        private async Task<bool> RunFfmpegAsync(
            string input,
            string output,
            string? coverPath,
            string title,
            string artist,
            string? album,
            int trackNumber,
            CancellationToken cancellationToken)
        {
            var args = new System.Text.StringBuilder();
            args.Append(CultureInfo.InvariantCulture, $"-y -i {Quote(input)} ");
            if (coverPath is not null)
            {
                args.Append(CultureInfo.InvariantCulture, $"-i {Quote(coverPath)} -map 0:a -map 1:v -disposition:v attached_pic ");
            }
            else
            {
                args.Append("-map 0:a ");
            }

            args.Append("-c copy ");
            args.Append(CultureInfo.InvariantCulture, $"-metadata title={Quote(title)} -metadata artist={Quote(artist)} ");
            if (!string.IsNullOrWhiteSpace(album))
            {
                args.Append(CultureInfo.InvariantCulture, $"-metadata album={Quote(album)} ");
            }

            if (trackNumber > 0)
            {
                args.Append(CultureInfo.InvariantCulture, $"-metadata track={trackNumber.ToString(CultureInfo.InvariantCulture)} ");
            }

            args.Append(Quote(output));

            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _mediaEncoder.EncoderPath,
                    Arguments = args.ToString(),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            proc.Start();
            await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (proc.ExitCode != 0)
            {
                var err = await proc.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("TrackTagger: ffmpeg exited {Code}: {Err}", proc.ExitCode, err.Length > 300 ? err[^300..] : err);
                return false;
            }

            return true;
        }

        private async Task<string?> DownloadCoverAsync(string? coverUrl, string dir, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(coverUrl))
            {
                return null;
            }

            if (!Uri.TryCreate(coverUrl, UriKind.Absolute, out var coverUri))
            {
                return null;
            }

            try
            {
                var client = _httpClientFactory.CreateClient("CadenceConfig");
                var bytes = await client.GetByteArrayAsync(coverUri, cancellationToken).ConfigureAwait(false);
                var path = Path.Combine(dir, $".cadence-cover-{Guid.NewGuid():N}.jpg");
                await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
                return path;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "TrackTagger: cover fetch failed; tagging without art.");
                return null;
            }
        }

#pragma warning disable CA1822 // instance (not static) to satisfy SA1204 ordering after instance members
        private string Quote(string s) => "\"" + s.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

        private void TryDelete(string? path)
        {
            if (path is not null && File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                    // best-effort temp cleanup
                }
            }
        }
#pragma warning restore CA1822
    }
}
