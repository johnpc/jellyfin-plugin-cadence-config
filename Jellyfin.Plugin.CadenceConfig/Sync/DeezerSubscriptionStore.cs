using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Sync
{
    /// <summary>
    /// Reads/writes the persisted Deezer→Jellyfin playlist subscriptions in the plugin configuration.
    /// Extracted from <see cref="DeezerImportService"/> so the import engine stays focused on matching
    /// and the config round-trip lives in one place. Live config I/O, so excluded from coverage.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class DeezerSubscriptionStore
    {
        /// <summary>Finds a user's subscription for a Deezer playlist id, or null.</summary>
        /// <param name="userId">The owning user id.</param>
        /// <param name="deezerId">The Deezer playlist id.</param>
        /// <returns>The subscription, or null.</returns>
        public static DeezerSubscription? Find(Guid userId, string deezerId) =>
            Array.Find(
                Plugin.GetConfiguration().DeezerSubscriptions,
                s => s.UserId == userId && string.Equals(s.DeezerPlaylistId, deezerId, StringComparison.Ordinal));

        /// <summary>Upserts a subscription (keyed by user + Deezer id) with its current missing artists.</summary>
        /// <param name="userId">The owning user id.</param>
        /// <param name="deezerId">The Deezer playlist id (no-op when empty).</param>
        /// <param name="playlistId">The mirrored Jellyfin playlist id.</param>
        /// <param name="missingArtists">The still-missing artist names to persist.</param>
        public static void Save(Guid userId, string deezerId, string playlistId, IReadOnlyList<string> missingArtists)
        {
            var plugin = Plugin.Instance;
            if (string.IsNullOrEmpty(deezerId) || plugin == null)
            {
                return;
            }

            plugin.Configuration.DeezerSubscriptions = plugin.Configuration.DeezerSubscriptions
                .Where(s => !(s.UserId == userId && string.Equals(s.DeezerPlaylistId, deezerId, StringComparison.Ordinal)))
                .Append(new DeezerSubscription
                {
                    UserId = userId,
                    DeezerPlaylistId = deezerId,
                    JellyfinPlaylistId = playlistId,
                    MissingArtists = missingArtists.ToArray(),
                })
                .ToArray();
            plugin.SaveConfiguration();
        }
    }
}
