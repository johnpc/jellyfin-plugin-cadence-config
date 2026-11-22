using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Chapters;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Thin Jellyfin library/user-data plumbing for audiobooks (enumerate AudioBook items, read and
    /// write a part's saved position). Every DECISION (grouping, ordering, offsets, which side wins)
    /// lives in the tested pure helpers; this class only moves data in and out of Jellyfin.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class JellyfinBooks
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IChapterManager _chapters;

        /// <summary>
        /// Initializes a new instance of the <see cref="JellyfinBooks"/> class.
        /// </summary>
        /// <param name="libraryManager">The library manager.</param>
        /// <param name="userManager">The user manager.</param>
        /// <param name="userDataManager">The user-data manager.</param>
        /// <param name="chapters">Jellyfin's chapter store (the m4b's embedded markers).</param>
        public JellyfinBooks(ILibraryManager libraryManager, IUserManager userManager, IUserDataManager userDataManager, IChapterManager chapters)
        {
            _libraryManager = libraryManager;
            _userManager = userManager;
            _userDataManager = userDataManager;
            _chapters = chapters;
        }

        /// <summary>The book's embedded chapter markers as global seconds (part offset + marker), ascending.</summary>
        /// <param name="parts">The parts in play order.</param>
        /// <returns>Marker times; empty when the files carry none.</returns>
        public IReadOnlyList<double> ChapterStarts(IReadOnlyList<BookPart> parts)
        {
            var marks = new List<double>();
            double offset = 0;
            foreach (var part in parts)
            {
                foreach (var chapter in _chapters.GetChapters(Guid.Parse(part.ItemId)))
                {
                    marks.Add(offset + PartsTimeline.TicksToSeconds(chapter.StartPositionTicks));
                }

                offset += part.Duration;
            }

            marks.Sort();
            return marks;
        }

        /// <summary>Every AudioBook item in the library as <see cref="PartInfo"/>.</summary>
        /// <returns>The items.</returns>
        public IReadOnlyList<PartInfo> AllParts()
        {
            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.AudioBook },
                Recursive = true,
            });
            return items.OfType<Audio>().Select(ToInfo).ToList();
        }

        /// <summary>The book (anchor + ordered parts) that contains an item, or null when it isn't an audiobook.</summary>
        /// <param name="itemId">Any part of the book.</param>
        /// <returns>The anchor info (first part) + parts, or null.</returns>
        public (PartInfo Anchor, IReadOnlyList<BookPart> Parts)? BookOf(Guid itemId)
        {
            if (_libraryManager.GetItemById(itemId) is not Audio audio)
            {
                return null;
            }

            var anchor = ToInfo(audio);
            var parts = PartOrder.PartsOf(anchor, AllParts());
            var first = parts[0].ItemId == anchor.ItemId ? anchor : (ToInfoOrNull(_libraryManager.GetItemById(Guid.Parse(parts[0].ItemId)) as Audio) ?? anchor);
            return (first, parts);
        }

        /// <summary>The author shown for a book: the first album artist, else the first artist.</summary>
        /// <param name="itemId">A part.</param>
        /// <returns>The author, or null.</returns>
        public string? AuthorOf(string itemId)
        {
            if (_libraryManager.GetItemById(Guid.Parse(itemId)) is not Audio audio)
            {
                return null;
            }

            return audio.AlbumArtists.Count > 0 ? audio.AlbumArtists[0] : audio.Artists.Count > 0 ? audio.Artists[0] : null;
        }

        /// <summary>The Jellyfin user, or null.</summary>
        /// <param name="userId">The id.</param>
        /// <returns>The user.</returns>
        public User? UserOf(Guid userId) => _userManager.GetUserById(userId);

        /// <summary>Saved positions of a book's parts for a user: (part id, seconds into part, last played).</summary>
        /// <param name="user">The user.</param>
        /// <param name="parts">The parts.</param>
        /// <returns>One entry per part.</returns>
        public IReadOnlyList<(string ItemId, double Seconds, DateTime? LastPlayed)> SavedPositions(User user, IReadOnlyList<BookPart> parts)
        {
            var result = new List<(string, double, DateTime?)>();
            foreach (var part in parts)
            {
                if (_libraryManager.GetItemById(Guid.Parse(part.ItemId)) is not BaseItem item)
                {
                    continue;
                }

                var data = _userDataManager.GetUserData(user, item);
                result.Add((part.ItemId, PartsTimeline.TicksToSeconds(data?.PlaybackPositionTicks), data?.LastPlayedDate));
            }

            return result;
        }

        /// <summary>Writes a resume position onto one part (and clears the other parts' positions).</summary>
        /// <param name="user">The user.</param>
        /// <param name="parts">The book's parts.</param>
        /// <param name="target">The part to resume in.</param>
        /// <param name="seconds">Seconds into that part.</param>
        /// <param name="lastPlayed">The stamp to record.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public void WritePosition(User user, IReadOnlyList<BookPart> parts, string target, double seconds, DateTime lastPlayed, CancellationToken cancellationToken)
        {
            foreach (var part in parts)
            {
                if (_libraryManager.GetItemById(Guid.Parse(part.ItemId)) is not BaseItem item)
                {
                    continue;
                }

                var isTarget = string.Equals(part.ItemId, target, StringComparison.OrdinalIgnoreCase);
                var data = _userDataManager.GetUserData(user, item) ?? new UserItemData { Key = item.GetUserDataKeys()[0] };
                if (!isTarget && data.PlaybackPositionTicks == 0)
                {
                    continue;
                }

                data.PlaybackPositionTicks = isTarget ? PartsTimeline.SecondsToTicks(seconds) : 0;
                if (isTarget)
                {
                    data.LastPlayedDate = lastPlayed;
                }

                _userDataManager.SaveUserData(user, item, data, UserDataSaveReason.UpdateUserData, cancellationToken);
            }
        }

        private static PartInfo ToInfo(Audio audio)
        {
            return new PartInfo(
                audio.Id.ToString("N"),
                audio.Name ?? string.Empty,
                audio.Album,
                audio.ParentId.ToString("N"),
                audio.IndexNumber,
                audio.RunTimeTicks);
        }

        private static PartInfo? ToInfoOrNull(Audio? audio) => audio == null ? null : ToInfo(audio);
    }
}
