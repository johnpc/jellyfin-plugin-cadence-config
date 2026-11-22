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
    /// <summary>The body of a position write.</summary>
    public sealed class ReadAlongPositionRequest
    {
        /// <summary>Gets or sets the position in global book seconds.</summary>
        public double Seconds { get; set; }

        /// <summary>Gets or sets the wall-clock stamp, ms since epoch.</summary>
        public long Timestamp { get; set; }
    }
}
