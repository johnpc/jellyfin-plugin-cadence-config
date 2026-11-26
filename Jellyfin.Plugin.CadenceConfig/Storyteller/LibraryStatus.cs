using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// The read-along status of one audiobook for the library listing, decided WITHOUT building a timeline:
    /// a matched, aligned book is <see cref="ReadAlongStatus.Ready"/> unless a timeline is already cached, in
    /// which case its real verdict (ready / mismatch / empty) is used. Building ~200 timelines to draw badges
    /// would cost minutes of Storyteller traffic; the per-book sheet still makes the full check.
    /// </summary>
    public static class LibraryStatus
    {
        /// <summary>Status of one book.</summary>
        /// <param name="book">The matched Storyteller book, or null.</param>
        /// <param name="reconciled">Its cached, reconciled timeline, or null when none is cached.</param>
        /// <param name="partsTotal">The Jellyfin audiobook's total length in seconds.</param>
        /// <returns>The status.</returns>
        public static ReadAlongStatus For(StorytellerBook? book, Timeline? reconciled, double partsTotal)
        {
            if (book == null)
            {
                return ReadAlongStatus.Unmatched;
            }

            if (string.IsNullOrEmpty(book.AlignedAt))
            {
                return ReadAlongStatus.Aligning;
            }

            return reconciled == null ? ReadAlongStatus.Ready : OverlayTimeline.Check(reconciled, partsTotal);
        }

        /// <summary>The response for a checked library.</summary>
        /// <param name="books">Each book's anchor id and status.</param>
        /// <returns>The response.</returns>
        public static ReadAlongLibrary Checked(IEnumerable<(string ItemId, ReadAlongStatus Status)> books)
        {
            var rows = books.Select(b => new LibraryBookStatus { ItemId = b.ItemId, Kind = b.Status }).ToList();
            return new ReadAlongLibrary
            {
                Kind = LibraryState.Ok,
                Books = rows,
                HasAligning = rows.Any(r => r.Kind == ReadAlongStatus.Aligning),
            };
        }

        /// <summary>The response when the library can't be checked.</summary>
        /// <param name="state">Why: <see cref="LibraryState.Unlinked"/> or <see cref="LibraryState.Unconfigured"/>.</param>
        /// <returns>The response with no books.</returns>
        public static ReadAlongLibrary Unavailable(LibraryState state)
        {
            return new ReadAlongLibrary { Kind = state, Books = Array.Empty<LibraryBookStatus>() };
        }
    }
}
