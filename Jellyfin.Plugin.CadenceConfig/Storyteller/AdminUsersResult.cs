using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The dashboard's view: the admin login state plus every Jellyfin user's row.</summary>
    public sealed class AdminUsersResult
    {
        /// <summary>Gets or sets a value indicating whether a Storyteller admin login is stored (auto-provisioning possible).</summary>
        public bool AdminConnected { get; set; }

        /// <summary>Gets or sets the admin account's username (display only).</summary>
        public string? AdminUsername { get; set; }

        /// <summary>Gets or sets the per-user rows.</summary>
        public IReadOnlyList<AdminUserRow> Users { get; set; } = Array.Empty<AdminUserRow>();
    }
}
