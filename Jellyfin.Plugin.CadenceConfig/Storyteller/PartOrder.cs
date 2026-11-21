using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Pure grouping/ordering of Jellyfin AudioBook items into one book's parts — the same rule the
    /// Cadence client uses (groupBooks): files sharing an Album tag are one book; a file without an
    /// Album is a book on its own. Parts play by IndexNumber, then Name.
    /// </summary>
    public static class PartOrder
    {
        /// <summary>The parts of the book that contains <c>anchor</c>, in play order.</summary>
        /// <param name="anchor">The item the client asked about.</param>
        /// <param name="candidates">AudioBook items that may belong to the same book (same folder is enough).</param>
        /// <returns>The ordered parts.</returns>
        public static IReadOnlyList<BookPart> PartsOf(PartInfo anchor, IEnumerable<PartInfo> candidates)
        {
            IEnumerable<PartInfo> siblings = string.IsNullOrWhiteSpace(anchor.Album)
                ? new[] { anchor }
                : candidates.Where(c => string.Equals(c.Album, anchor.Album, StringComparison.OrdinalIgnoreCase));

            var ordered = siblings
                .Concat(new[] { anchor })
                .GroupBy(p => p.ItemId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(p => p.IndexNumber ?? 0)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .ToList();

            return ordered.Select(p => new BookPart(p.ItemId, PartsTimeline.TicksToSeconds(p.RunTimeTicks))).ToList();
        }

        /// <summary>Groups a library's AudioBook items into books (one entry per Album, loose files alone).</summary>
        /// <param name="items">Every AudioBook item.</param>
        /// <returns>Each book's parts in play order, keyed by its anchor (first part).</returns>
        public static IReadOnlyList<(PartInfo Anchor, IReadOnlyList<BookPart> Parts)> Books(IReadOnlyList<PartInfo> items)
        {
            var result = new List<(PartInfo, IReadOnlyList<BookPart>)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                var key = string.IsNullOrWhiteSpace(item.Album) ? "item:" + item.ItemId : "album:" + item.Album;
                if (!seen.Add(key))
                {
                    continue;
                }

                var parts = PartsOf(item, items);
                var anchor = items.First(i => string.Equals(i.ItemId, parts[0].ItemId, StringComparison.OrdinalIgnoreCase));
                result.Add((anchor, parts));
            }

            return result;
        }
    }
}
