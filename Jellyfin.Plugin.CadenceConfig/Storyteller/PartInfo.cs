using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The fields of a Jellyfin part that decide its play order and grouping.</summary>
    /// <param name="ItemId">The item id.</param>
    /// <param name="Name">The item name.</param>
    /// <param name="Album">The shared book title tag (empty for a loose file).</param>
    /// <param name="ParentId">The folder.</param>
    /// <param name="IndexNumber">The track number.</param>
    /// <param name="RunTimeTicks">The length.</param>
    public sealed record PartInfo(string ItemId, string Name, string? Album, string ParentId, int? IndexNumber, long? RunTimeTicks);
}
