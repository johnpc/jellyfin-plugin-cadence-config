using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>The result of importing a Deezer playlist: its title and every track.</summary>
    /// <param name="Title">The playlist title.</param>
    /// <param name="Tracks">All tracks (across all pages).</param>
    public sealed record DeezerImport(string Title, IReadOnlyList<DeezerTrack> Tracks);
}
