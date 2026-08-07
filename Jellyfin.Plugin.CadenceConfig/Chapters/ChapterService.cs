using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Chapters
{
    /// <summary>
    /// Extracts embedded chapter markers from an audiobook's media file. Jellyfin recognises m4b files
    /// as audiobooks and probes their duration, but it does NOT surface the embedded chapter atoms on
    /// its item API for audio — and <see cref="IMediaEncoder.GetMediaInfo"/> with <c>ExtractChapters</c>
    /// returns none for audio too (verified on the live server: a 12-chapter m4b returned nothing). So
    /// this service shells out to ffprobe DIRECTLY (Jellyfin's bundled binary at
    /// <see cref="IMediaEncoder.ProbePath"/>) with <c>-show_chapters</c> and parses its JSON via the pure
    /// <see cref="FfprobeChapters"/>. Results are cached per item id since a file's chapters never change.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class ChapterService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly ILogger<ChapterService> _logger;
        private readonly Dictionary<Guid, IReadOnlyList<AudiobookChapter>> _cache = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ChapterService"/> class.
        /// </summary>
        /// <param name="libraryManager">Jellyfin library manager (to resolve the item + its file).</param>
        /// <param name="mediaEncoder">Jellyfin's media encoder (for the bundled ffprobe path).</param>
        /// <param name="logger">The logger.</param>
        public ChapterService(
            ILibraryManager libraryManager,
            IMediaEncoder mediaEncoder,
            ILogger<ChapterService> logger)
        {
            _libraryManager = libraryManager;
            _mediaEncoder = mediaEncoder;
            _logger = logger;
        }

        /// <summary>
        /// Get the chapters for an audiobook item, probing its file on first request and caching after.
        /// Returns null when the item id doesn't resolve to a file we can probe; an empty list when the
        /// file simply has no (usable) chapters.
        /// </summary>
        /// <param name="itemId">The Jellyfin item id of the audiobook file.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The chapters, an empty list, or null if the item can't be resolved.</returns>
        public async Task<IReadOnlyList<AudiobookChapter>?> GetChaptersAsync(
            Guid itemId,
            CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(itemId, out var cached))
            {
                return cached;
            }

            var item = _libraryManager.GetItemById(itemId);
            var path = item?.Path;
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                var json = await RunProbeAsync(path, cancellationToken).ConfigureAwait(false);
                var chapters = FfprobeChapters.Parse(json);
                _cache[itemId] = chapters;
                return chapters;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Probing is best-effort — a bad/unreadable file must not 500 the endpoint.
                // (A cancellation propagates; anything else degrades to "no chapters".)
                _logger.LogWarning(ex, "Failed to extract chapters for item {ItemId}", itemId);
                return new List<AudiobookChapter>();
            }
        }

        /// <summary>Run Jellyfin's bundled ffprobe with -show_chapters and return its stdout JSON.</summary>
        private async Task<string> RunProbeAsync(string path, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _mediaEncoder.ProbePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-print_format");
            psi.ArgumentList.Add("json");
            psi.ArgumentList.Add("-show_chapters");
            psi.ArgumentList.Add(path);

            using var process = new Process { StartInfo = psi };
            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return stdout;
        }
    }
}
