using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Audiobooks
{
    /// <summary>
    /// Builds and caches the whole audiobook library as client-ready DTOs, so the Cadence client fetches
    /// it in ONE fast call instead of running its own recursive AudioBook scan (up to 5000 files,
    /// 4–19s on a large library) on every visit. The heavy scan runs once; the result is held in memory
    /// and served instantly. The cache is invalidated on library changes (AudiobookLibraryInvalidator
    /// hooks ItemAdded/Updated/Removed) and refreshed by a scheduled task, so it stays current without
    /// re-scanning per request. Thin I/O plumbing around Jellyfin's library + DTO services — the
    /// query/sort shape is asserted via AudiobookLibraryFields; excluded from coverage like the other
    /// service shims (ChapterService, PlaylistCoverService).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class AudiobookLibraryService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IDtoService _dtoService;
        private readonly ILogger<AudiobookLibraryService> _logger;
        private readonly object _lock = new();
        private IReadOnlyList<BaseItemDto>? _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudiobookLibraryService"/> class.
        /// </summary>
        /// <param name="libraryManager">Jellyfin library manager (enumerate AudioBook items).</param>
        /// <param name="dtoService">Jellyfin DTO service (build client-ready item DTOs).</param>
        /// <param name="logger">The logger.</param>
        public AudiobookLibraryService(
            ILibraryManager libraryManager,
            IDtoService dtoService,
            ILogger<AudiobookLibraryService> logger)
        {
            _libraryManager = libraryManager;
            _dtoService = dtoService;
            _logger = logger;
        }

        /// <summary>
        /// Get the cached audiobook library, building it on the first request (or after invalidation).
        /// Subsequent calls return the in-memory list with no library scan.
        /// </summary>
        /// <returns>The audiobook DTOs, SortName ascending.</returns>
        public IReadOnlyList<BaseItemDto> GetLibrary()
        {
            var cached = _cache;
            if (cached is not null)
            {
                return cached;
            }

            lock (_lock)
            {
                return _cache ??= Build();
            }
        }

        /// <summary>
        /// Rebuild the cache now (used by the scheduled refresh task) and return the fresh list.
        /// </summary>
        /// <returns>The freshly-built audiobook DTOs.</returns>
        public IReadOnlyList<BaseItemDto> Rebuild()
        {
            lock (_lock)
            {
                return _cache = Build();
            }
        }

        /// <summary>
        /// Drop the cache so the next <see cref="GetLibrary"/> rebuilds it. Called when the library
        /// changes — cheap, so we don't rebuild eagerly on every single item event.
        /// </summary>
        public void Invalidate()
        {
            _cache = null;
        }

        private IReadOnlyList<BaseItemDto> Build()
        {
            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.AudioBook },
                Recursive = true,
                OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) },
            });

            var options = new DtoOptions(false) { Fields = AudiobookLibraryFields.Fields };
            var dtos = _dtoService.GetBaseItemDtos(items, options);
            _logger.LogInformation("Audiobook library: cached {Count} book file(s).", dtos.Count);
            return dtos;
        }
    }
}
