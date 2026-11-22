using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>One playable part (file) of a Jellyfin audiobook, in play order.</summary>
    /// <param name="ItemId">The Jellyfin item id (N format).</param>
    /// <param name="Duration">The part's length in seconds.</param>
    public sealed record BookPart(string ItemId, double Duration);
}
