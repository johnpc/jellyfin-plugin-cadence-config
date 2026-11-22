using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>The newer of the two sides' positions.</summary>
    public sealed class ReadAlongPositionResult
    {
        /// <summary>Gets or sets global seconds.</summary>
        public double Seconds { get; set; }

        /// <summary>Gets or sets the stamp, ms since epoch.</summary>
        public long Timestamp { get; set; }

        /// <summary>Gets or sets "storyteller" or "jellyfin".</summary>
        public string Source { get; set; } = string.Empty;
    }
}
