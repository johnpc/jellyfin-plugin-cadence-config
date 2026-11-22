using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>A position on the global timeline with where it came from.</summary>
    /// <param name="Seconds">Global seconds.</param>
    /// <param name="Timestamp">Wall-clock stamp, ms since epoch.</param>
    /// <param name="Source">"storyteller" or "jellyfin".</param>
    public sealed record MergedPosition(double Seconds, long Timestamp, string Source);
}
