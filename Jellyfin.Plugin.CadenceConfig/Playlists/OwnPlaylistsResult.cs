using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Playlists
{
    /// <summary>
    /// The response of GET /Cadence/Playlists/Mine: the calling user's own playlist ids.
    /// </summary>
    public class OwnPlaylistsResult
    {
        /// <summary>
        /// Gets or sets the ids of the playlists the caller owns.
        /// </summary>
        public IReadOnlyList<string> Ids { get; set; } = new List<string>();
    }
}
