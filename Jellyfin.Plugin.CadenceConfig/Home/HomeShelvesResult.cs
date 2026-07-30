using System.Collections.Generic;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.CadenceConfig.Home
{
    /// <summary>
    /// The precomputed Home shelves for one user, returned by GET /Cadence/Home in a SINGLE response
    /// so the Cadence client skips ~6 slow recursive library scans. Each list holds the same
    /// <see cref="BaseItemDto"/> shape the client already consumes from Jellyfin's /Items endpoint,
    /// so the client maps it with no special-casing (see the client's homeSource adapter). Property
    /// names are PascalCase to match Jellyfin's serializer and the client's HomeResponse fields.
    /// </summary>
    public class HomeShelvesResult
    {
        /// <summary>Gets the recently-added albums ("Recently added" shelf).</summary>
        public IReadOnlyList<BaseItemDto> LatestAlbums { get; init; } = new List<BaseItemDto>();

        /// <summary>Gets the suggested songs ("Suggested for you" shelf).</summary>
        public IReadOnlyList<BaseItemDto> SuggestedSongs { get; init; } = new List<BaseItemDto>();

        /// <summary>Gets the user's favorited albums ("From your library" shelf).</summary>
        public IReadOnlyList<BaseItemDto> SavedAlbums { get; init; } = new List<BaseItemDto>();

        /// <summary>Gets the most-recently-played songs ("Recently played" shelf).</summary>
        public IReadOnlyList<BaseItemDto> RecentlyPlayed { get; init; } = new List<BaseItemDto>();

        /// <summary>Gets the user's most-played songs ("On repeat" shelf).</summary>
        public IReadOnlyList<BaseItemDto> OnRepeat { get; init; } = new List<BaseItemDto>();

        /// <summary>Gets the user's followed (favorited) artists ("Your artists" shelf).</summary>
        public IReadOnlyList<BaseItemDto> FollowedArtists { get; init; } = new List<BaseItemDto>();
    }
}
