using Jellyfin.Database.Implementations.Entities;

namespace Jellyfin.Plugin.CadenceConfig.Home
{
    /// <summary>
    /// Computes a user's Home shelves. Extracted so consumers (the background
    /// <see cref="HomeShelvesRefresher"/>, the daily task) depend on an interface and can be
    /// unit-tested with a fake, rather than the sealed concrete service that needs real Jellyfin
    /// library/DTO managers.
    /// </summary>
    public interface IHomeShelvesService
    {
        /// <summary>Builds every Home shelf for the given user.</summary>
        /// <param name="user">The user whose shelves to compute.</param>
        /// <returns>The precomputed shelves.</returns>
        HomeShelvesResult Build(User user);
    }
}
