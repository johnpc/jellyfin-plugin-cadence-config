using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>
    /// Splits a Deezer playlist's tracks against what a Jellyfin library owns: the tracks present
    /// (returned as the owning library item ids, to add to a Jellyfin playlist) and the tracks
    /// missing (returned as distinct artist names, to offer for a Lidarr request). Pure so the whole
    /// match is unit-testable without a live server.
    /// </summary>
    public static class DeezerMatcher
    {
        /// <summary>
        /// Matches Deezer tracks against a library index keyed by normalized (artist, title).
        /// </summary>
        /// <param name="deezerTracks">The Deezer playlist's tracks.</param>
        /// <param name="libraryByKey">Map of the library's TrackKey → Jellyfin item id.</param>
        /// <returns>The found item ids (dedup'd, in playlist order) and missing artist names.</returns>
        public static DeezerMatchResult Match(
            IEnumerable<DeezerTrack> deezerTracks,
            IReadOnlyDictionary<TrackKey, string> libraryByKey)
        {
            var foundIds = new List<string>();
            var seenIds = new HashSet<string>();
            var missingArtists = new List<string>();
            var seenMissing = new HashSet<string>();

            foreach (var t in deezerTracks)
            {
                var artist = t.Artist?.Name;
                var key = new TrackKey(artist, t.Title);
                if (!key.IsValid)
                {
                    continue;
                }

                if (libraryByKey.TryGetValue(key, out var id))
                {
                    if (seenIds.Add(id))
                    {
                        foundIds.Add(id);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(artist) && seenMissing.Add(TrackKey.Normalize(artist)))
                {
                    missingArtists.Add(artist.Trim());
                }
            }

            return new DeezerMatchResult(foundIds, missingArtists);
        }
    }
}
