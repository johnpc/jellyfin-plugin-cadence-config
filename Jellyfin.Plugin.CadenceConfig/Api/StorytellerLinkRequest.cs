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
    /// <summary>The request body for connecting a Storyteller account.</summary>
    public sealed class StorytellerLinkRequest
    {
        /// <summary>Gets or sets the Storyteller username or email.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Gets or sets the Storyteller password (exchanged once for a token, never stored).</summary>
        public string Password { get; set; } = string.Empty;
    }
}
