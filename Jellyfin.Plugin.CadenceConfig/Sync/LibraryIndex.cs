using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.CadenceConfig.Sync
{
    /// <summary>
    /// Builds the normalized (artist, title) → item id index of a user's audio library that the
    /// <see cref="DeezerMatcher"/> resolves Deezer tracks against. Extracted from the import service so
    /// each file stays within the line limit; the mapping itself is straight library plumbing.
    /// </summary>
    public static class LibraryIndex
    {
        /// <summary>
        /// Builds the TrackKey → item id map for the user's audio items. First key wins on collisions.
        /// </summary>
        /// <param name="libraryManager">The Jellyfin library manager to enumerate audio items.</param>
        /// <param name="userId">The user whose library to index (reserved for future per-user scoping).</param>
        /// <returns>The TrackKey → item id map.</returns>
        public static Dictionary<TrackKey, string> Build(ILibraryManager libraryManager, Guid userId)
        {
            _ = userId;
            var items = libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Audio },
                Recursive = true,
            });

            var index = new Dictionary<TrackKey, string>();
            foreach (var item in items.OfType<Audio>())
            {
                var artist = item.Artists.Count > 0
                    ? item.Artists[0]
                    : item.AlbumArtists.Count > 0 ? item.AlbumArtists[0] : null;
                var key = new TrackKey(artist, item.Name);
                if (key.IsValid && !index.ContainsKey(key))
                {
                    index[key] = item.Id.ToString("N");
                }
            }

            return index;
        }
    }
}
