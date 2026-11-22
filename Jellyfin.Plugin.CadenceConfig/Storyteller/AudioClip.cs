namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>A parsed audio clip reference (<see cref="Audioref.Parse"/>).</summary>
    /// <param name="Path">The audio path as written (unresolved).</param>
    /// <param name="Start">Clip start, seconds into the file.</param>
    /// <param name="End">Clip end, seconds into the file (null when open-ended).</param>
    public sealed record AudioClip(string Path, double Start, double? End);
}
