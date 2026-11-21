using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>The caller's link, as the client sees it (never the token).</summary>
    public sealed class StorytellerLinkResult
    {
        /// <summary>Gets or sets a value indicating whether the caller has a Storyteller link.</summary>
        public bool Linked { get; set; }

        /// <summary>Gets or sets the linked Storyteller username (null when unlinked).</summary>
        public string? Username { get; set; }
    }
}
