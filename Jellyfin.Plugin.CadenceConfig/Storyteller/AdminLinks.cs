using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CadenceConfig.Configuration;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// The admin view of links: one row per Jellyfin user, what the admin may be shown, and the wire
    /// spelling of <see cref="LinkKind"/>. Pure, so the admin controller stays plumbing.
    /// </summary>
    public static class AdminLinks
    {
        /// <summary>The wire spelling of a kind ("mapped" / "provisioned").</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>The lowercase name.</returns>
        public static string Wire(LinkKind kind) => kind == LinkKind.Provisioned ? "provisioned" : "mapped";

        /// <summary>One row per Jellyfin user, in the given order, with its link (if any) — never the token.</summary>
        /// <param name="users">(id, username) of every Jellyfin user.</param>
        /// <param name="config">The configuration holding the links.</param>
        /// <returns>The rows.</returns>
        public static IReadOnlyList<AdminUserRow> Rows(IEnumerable<(Guid Id, string Username)> users, PluginConfiguration config)
        {
            return users.Select(u => RowFor(u.Id, u.Username, LinkStore.Find(config, u.Id))).ToList();
        }

        /// <summary>The row for one user.</summary>
        /// <param name="id">The Jellyfin user id.</param>
        /// <param name="username">The Jellyfin username.</param>
        /// <param name="link">The user's link, or null.</param>
        /// <returns>The row.</returns>
        public static AdminUserRow RowFor(Guid id, string username, StorytellerLink? link) => new()
        {
            JellyfinUserId = id,
            JellyfinUsername = username,
            Linked = link != null,
            StorytellerUsername = link?.Username,
            Kind = link == null ? null : Wire(link.Kind),
        };

        /// <summary>
        /// The login the admin may hand to a person: only for accounts the plugin created (a mapped
        /// account's password was never kept, and is not ours to reveal).
        /// </summary>
        /// <param name="link">The link, or null.</param>
        /// <returns>The credentials, or null.</returns>
        public static ProvisionedCredentials? Credentials(StorytellerLink? link)
        {
            if (link == null || link.Kind != LinkKind.Provisioned || string.IsNullOrEmpty(link.Password))
            {
                return null;
            }

            return new ProvisionedCredentials { Username = link.Username, Password = link.Password };
        }

        /// <summary>A link for an existing Storyteller account the admin mapped (password not kept).</summary>
        /// <param name="jellyfinUserId">The Jellyfin user.</param>
        /// <param name="storytellerUsername">The Storyteller username.</param>
        /// <param name="token">The token the login produced.</param>
        /// <returns>The link.</returns>
        public static StorytellerLink Mapped(Guid jellyfinUserId, string storytellerUsername, string token) => new()
        {
            JellyfinUserId = jellyfinUserId,
            Username = storytellerUsername,
            Token = token,
            Kind = LinkKind.Mapped,
        };
    }
}
