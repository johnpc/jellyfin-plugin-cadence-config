using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Configuration;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>Everything resolved about one (caller, audiobook) pair, for the controllers and the bridge.</summary>
    /// <param name="Link">The caller's Storyteller link.</param>
    /// <param name="BaseUrl">The normalized Storyteller base URL.</param>
    /// <param name="Anchor">The book's first part.</param>
    /// <param name="Parts">The book's parts in play order.</param>
    /// <param name="Book">The matched Storyteller book (null = unmatched).</param>
    /// <param name="Timeline">The built timeline (null unless aligned and buildable).</param>
    /// <param name="Status">The explicit status.</param>
    public sealed record ResolvedBook(
        StorytellerLink Link,
        string BaseUrl,
        PartInfo Anchor,
        IReadOnlyList<BookPart> Parts,
        StorytellerBook? Book,
        Timeline? Timeline,
        ReadAlongStatus Status);
}
