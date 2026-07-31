using Jellyfin.Database.Implementations.Entities;

namespace Jellyfin.Plugin.CadenceConfig.Audiobooks
{
    /// <summary>
    /// Computes a user's audiobook library. Extracted so consumers (the background
    /// <see cref="AudiobooksRefresher"/>, the daily task) depend on an interface and can be
    /// unit-tested with a fake, rather than the sealed concrete service that needs real Jellyfin
    /// library/DTO managers.
    /// </summary>
    public interface IAudiobooksService
    {
        /// <summary>Builds the full audiobook library for the given user.</summary>
        /// <param name="user">The user whose library to compute.</param>
        /// <returns>The precomputed audiobook list.</returns>
        AudiobooksResult Build(User user);
    }
}
