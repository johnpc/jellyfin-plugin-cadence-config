using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

namespace Jellyfin.Plugin.CadenceConfig.Audiobooks
{
    /// <summary>
    /// Computes a user's full audiobook library in one pass. This is the SAME query the Cadence
    /// client's getAudiobooks() runs today — every AudioBook file, recursive, sorted by SortName —
    /// but batched here (server-side, no tunnel round trips) and cached by
    /// <see cref="AudiobooksCache"/>, so the client fetches the whole library in one fast call
    /// instead of a slow recursive scan of up to 5000 files (7–19s on a large library). Items are
    /// serialized to <see cref="BaseItemDto"/> via <see cref="IDtoService"/> so the shape matches
    /// /Items exactly and the client's groupBooks logic works unchanged.
    /// </summary>
    // Excluded from coverage: thin, hard-to-unit-test Jellyfin library-query plumbing (needs a real
    // ILibraryManager + IDtoService). Verified via the live deploy, not units — same rationale as
    // HomeShelvesService. The testable orchestration lives in AudiobooksRefresher (behind
    // IAudiobooksService), which IS covered.
    [ExcludeFromCodeCoverage]
    public sealed class AudiobooksService : IAudiobooksService
    {
        // Must exceed the library's file count (900+ across all books) or later books silently drop
        // off the end — the client's grouping needs ALL parts to build each multi-file book. Mirrors
        // the client's getAudiobooks(limit = 5000).
        private const int LibraryLimit = 5000;
        private readonly ILibraryManager _libraryManager;
        private readonly IDtoService _dtoService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudiobooksService"/> class.
        /// </summary>
        /// <param name="libraryManager">The library manager used to run the library query.</param>
        /// <param name="dtoService">The DTO service used to serialize items like /Items does.</param>
        public AudiobooksService(ILibraryManager libraryManager, IDtoService dtoService)
        {
            _libraryManager = libraryManager;
            _dtoService = dtoService;
        }

        // Overview drives the book detail; Album/AlbumArtist/ParentId/IndexNumber (multi-file
        // grouping) and Artists ride on the DTO. Image tags feed the shelf cards.
        private static DtoOptions Fields => new DtoOptions(false)
        {
            Fields = new[] { ItemFields.Overview, ItemFields.PrimaryImageAspectRatio },
            ImageTypeLimit = 1,
            EnableImages = true,
        };

        /// <summary>
        /// Builds the full audiobook library for the given user.
        /// </summary>
        /// <param name="user">The user whose library to compute.</param>
        /// <returns>The precomputed audiobook list.</returns>
        public AudiobooksResult Build(User user)
        {
            var query = new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.AudioBook },
                Recursive = true,
                Limit = LibraryLimit,
                OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) },
            };
            var items = _libraryManager.GetItemList(query);
            return new AudiobooksResult
            {
                Books = items.Select(i => _dtoService.GetBaseItemDto(i, Fields, user)).ToList(),
            };
        }
    }
}
