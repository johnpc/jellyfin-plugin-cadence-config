using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Covers
{
    /// <summary>
    /// Pure selection of the source images for a playlist's generated mosaic cover: from the primary
    /// image path of each track in order (a track's own image, else its album's — the caller resolves
    /// that), pick up to four DISTINCT paths so the mosaic isn't four copies of one album. Kept free
    /// of Jellyfin entities and I/O so it's fully unit-testable; the service resolves the paths and
    /// does the drawing/saving.
    /// </summary>
    public static class CoverSelection
    {
        /// <summary>
        /// The distinct image paths to tile, in order, capped at <paramref name="max"/>. Null/empty
        /// entries (a track with no usable art) are skipped, and duplicates are dropped so four tiles
        /// are four different covers. Returns fewer than <paramref name="max"/> (even zero) when the
        /// input lacks that much distinct art — the caller decides (1-up, or skip).
        /// </summary>
        /// <param name="trackImagePaths">Each track's resolved primary image path (null when none), in order.</param>
        /// <param name="max">The maximum number of tiles (4 for a 2×2 mosaic).</param>
        /// <returns>Up to <paramref name="max"/> distinct image paths.</returns>
        public static IReadOnlyList<string> PickTiles(IReadOnlyList<string?> trackImagePaths, int max = 4)
        {
            ArgumentNullException.ThrowIfNull(trackImagePaths);

            var paths = new List<string>(max);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in trackImagePaths)
            {
                if (string.IsNullOrEmpty(path) || !seen.Add(path))
                {
                    continue;
                }

                paths.Add(path);
                if (paths.Count >= max)
                {
                    break;
                }
            }

            return paths;
        }
    }
}
