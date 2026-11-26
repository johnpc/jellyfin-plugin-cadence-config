using System;
using System.Collections.Concurrent;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// The library listing per caller, reused for <see cref="Ttl"/> (Books tab opens, focus refetches and the
    /// Continue-listening shelf all ask). The previous answer also says whether a book was still aligning, so
    /// the next build re-reads Storyteller's book list on the short TTL and a finished alignment shows up fast.
    /// </summary>
    public sealed class LibraryResponseCache
    {
        /// <summary>How long a caller's listing is reused.</summary>
        public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

        private readonly Func<DateTimeOffset> _now;
        private readonly ConcurrentDictionary<Guid, (DateTimeOffset At, ReadAlongLibrary Value)> _entries = new();

        /// <summary>Initializes a new instance of the <see cref="LibraryResponseCache"/> class.</summary>
        public LibraryResponseCache()
            : this(() => DateTimeOffset.UtcNow)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="LibraryResponseCache"/> class with a clock.</summary>
        /// <param name="now">The clock.</param>
        public LibraryResponseCache(Func<DateTimeOffset> now)
        {
            _now = now;
        }

        /// <summary>The caller's listing when it is younger than <see cref="Ttl"/>, else null.</summary>
        /// <param name="caller">The caller.</param>
        /// <returns>The cached listing, or null.</returns>
        public ReadAlongLibrary? Fresh(Guid caller)
        {
            return _entries.TryGetValue(caller, out var e) && _now() - e.At < Ttl ? e.Value : null;
        }

        /// <summary>Whether the caller's last listing (fresh or not) had a book still aligning.</summary>
        /// <param name="caller">The caller.</param>
        /// <returns>True when a book was aligning last time.</returns>
        public bool WasAligning(Guid caller) => _entries.TryGetValue(caller, out var e) && e.Value.HasAligning;

        /// <summary>Stores the caller's listing.</summary>
        /// <param name="caller">The caller.</param>
        /// <param name="value">The listing.</param>
        public void Put(Guid caller, ReadAlongLibrary value) => _entries[caller] = (_now(), value);
    }
}
