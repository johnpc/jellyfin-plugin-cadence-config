using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>
    /// What the client gets back after importing a Deezer playlist: the created Jellyfin playlist,
    /// how many tracks were added, and the artists whose tracks aren't in the library yet (which the
    /// client offers to request via Lidarr).
    /// </summary>
    public sealed class DeezerImportResult
    {
        /// <summary>Gets or sets the created/updated Jellyfin playlist id.</summary>
        public string PlaylistId { get; set; } = string.Empty;

        /// <summary>Gets or sets the playlist name (mirrors the Deezer playlist title).</summary>
        public string PlaylistName { get; set; } = string.Empty;

        /// <summary>Gets or sets how many library tracks were added to the playlist.</summary>
        public int AddedCount { get; set; }

        /// <summary>Gets or sets the total tracks in the source Deezer playlist.</summary>
        public int TotalCount { get; set; }

        /// <summary>Gets or sets the distinct artists whose tracks are missing (Lidarr candidates).</summary>
        public IReadOnlyList<string> MissingArtists { get; set; } = new List<string>();
    }
}
