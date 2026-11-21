using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Maps between a part-local position (what Jellyfin stores per file) and the book's GLOBAL
    /// timeline (what Storyteller's alignment is measured on): global = Σ earlier parts + offset.
    /// Pure; both the plugin and the Cadence client derive offsets from the same <see cref="BookPart"/>
    /// list the plugin serves, so they can never disagree.
    /// </summary>
    public static class PartsTimeline
    {
        /// <summary>The whole book's length.</summary>
        /// <param name="parts">The parts in play order.</param>
        /// <returns>Seconds.</returns>
        public static double Total(IReadOnlyList<BookPart> parts) => parts.Sum(p => p.Duration);

        /// <summary>Global seconds for a position inside one part; null when the part is unknown.</summary>
        /// <param name="parts">The parts in play order.</param>
        /// <param name="itemId">The part.</param>
        /// <param name="offsetSeconds">Seconds into that part.</param>
        /// <returns>Global seconds, or null.</returns>
        public static double? ToGlobal(IReadOnlyList<BookPart> parts, string itemId, double offsetSeconds)
        {
            double start = 0;
            foreach (var part in parts)
            {
                if (string.Equals(part.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    return start + Math.Max(0, offsetSeconds);
                }

                start += part.Duration;
            }

            return null;
        }

        /// <summary>The part + offset that holds a global position (clamped to the last part).</summary>
        /// <param name="parts">The parts in play order.</param>
        /// <param name="globalSeconds">Global seconds.</param>
        /// <returns>The part and the offset into it, or null for an empty book.</returns>
        public static (BookPart Part, double Offset)? Locate(IReadOnlyList<BookPart> parts, double globalSeconds)
        {
            if (parts.Count == 0)
            {
                return null;
            }

            var remaining = Math.Max(0, globalSeconds);
            foreach (var part in parts)
            {
                if (remaining < part.Duration)
                {
                    return (part, remaining);
                }

                remaining -= part.Duration;
            }

            var last = parts[^1];
            return (last, Math.Max(0, last.Duration));
        }

        /// <summary>Jellyfin ticks (100 ns) to seconds.</summary>
        /// <param name="ticks">Ticks.</param>
        /// <returns>Seconds.</returns>
        public static double TicksToSeconds(long? ticks) => (ticks ?? 0) / 10_000_000d;

        /// <summary>Seconds to Jellyfin ticks.</summary>
        /// <param name="seconds">Seconds.</param>
        /// <returns>Ticks.</returns>
        public static long SecondsToTicks(double seconds) => (long)Math.Round(Math.Max(0, seconds) * 10_000_000d);
    }
}
