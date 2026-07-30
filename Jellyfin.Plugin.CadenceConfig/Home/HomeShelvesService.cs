using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.CadenceConfig.Home
{
    /// <summary>
    /// Computes a user's Home shelves in one pass. Each shelf is the SAME query Jellyfin's /Items
    /// endpoint runs for the Cadence client today — but batched here (server-side, no tunnel round
    /// trips) and cached by <see cref="HomeShelvesCache"/>, so the client fetches all six shelves in
    /// one fast call instead of ~6 slow recursive library scans. Items are serialized to
    /// <see cref="BaseItemDto"/> via <see cref="IDtoService"/> so the shape matches /Items exactly.
    /// </summary>
    public sealed class HomeShelvesService
    {
        private const int ShelfLimit = 20;
        private readonly ILibraryManager _libraryManager;
        private readonly IDtoService _dtoService;

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeShelvesService"/> class.
        /// </summary>
        /// <param name="libraryManager">The library manager used to run the shelf queries.</param>
        /// <param name="dtoService">The DTO service used to serialize items like /Items does.</param>
        public HomeShelvesService(ILibraryManager libraryManager, IDtoService dtoService)
        {
            _libraryManager = libraryManager;
            _dtoService = dtoService;
        }

        // Artists/AlbumArtists ride on the DTO by default; we only need image tags for the cards.
        private static DtoOptions Fields => new DtoOptions(false)
        {
            Fields = new[] { ItemFields.PrimaryImageAspectRatio },
            ImageTypeLimit = 1,
            EnableImages = true,
        };

        /// <summary>
        /// Builds every Home shelf for the given user.
        /// </summary>
        /// <param name="user">The user whose shelves to compute.</param>
        /// <returns>The precomputed shelves.</returns>
        public HomeShelvesResult Build(User user)
        {
            return new HomeShelvesResult
            {
                LatestAlbums = Query(user, BaseItemKind.MusicAlbum, ItemSortBy.DateCreated, null),
                SuggestedSongs = Query(user, BaseItemKind.Audio, ItemSortBy.Random, null),
                SavedAlbums = Query(user, BaseItemKind.MusicAlbum, ItemSortBy.SortName, true),
                RecentlyPlayed = Query(user, BaseItemKind.Audio, ItemSortBy.DatePlayed, null, played: true),
                OnRepeat = Query(user, BaseItemKind.Audio, ItemSortBy.PlayCount, null, played: true),
                FollowedArtists = QueryArtists(user),
            };
        }

        private IReadOnlyList<BaseItemDto> Query(
            User user, BaseItemKind kind, ItemSortBy sortBy, bool? favorite, bool played = false)
        {
            var query = new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { kind },
                Recursive = true,
                Limit = ShelfLimit,
                OrderBy = new[] { (sortBy, SortOrder.Descending) },
                IsFavorite = favorite,
                IsPlayed = played ? true : (bool?)null,
            };
            var items = _libraryManager.GetItemList(query);
            return ToDtos(items, user);
        }

        private IReadOnlyList<BaseItemDto> QueryArtists(User user)
        {
            // Followed = favorited artists. Artists live off the generic item query on this server,
            // so filter MusicArtist by IsFavorite (mirrors the client's /Artists?IsFavorite call).
            var query = new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.MusicArtist },
                Recursive = true,
                Limit = ShelfLimit,
                OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) },
                IsFavorite = true,
            };
            return ToDtos(_libraryManager.GetItemList(query), user);
        }

        private IReadOnlyList<BaseItemDto> ToDtos(IReadOnlyList<BaseItem> items, User user)
        {
            return items.Select(i => _dtoService.GetBaseItemDto(i, Fields, user)).ToList();
        }
    }
}
