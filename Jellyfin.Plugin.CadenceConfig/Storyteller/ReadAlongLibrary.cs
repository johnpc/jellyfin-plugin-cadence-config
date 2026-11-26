using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The wire response for <c>GET /Cadence/Storyteller/Books</c>: read-along status of every audiobook.</summary>
    public sealed class ReadAlongLibrary
    {
        /// <summary>Gets or sets each audiobook's status (empty unless <see cref="Kind"/> is <see cref="LibraryState.Ok"/>).</summary>
        public IReadOnlyList<LibraryBookStatus> Books { get; set; } = Array.Empty<LibraryBookStatus>();

        /// <summary>Gets or sets whether the library could be checked.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public LibraryState Kind { get; set; }

        /// <summary>Gets the library state as the client expects it: ok | unlinked | unconfigured.</summary>
        public string Status => Kind switch
        {
            LibraryState.Unlinked => "unlinked",
            LibraryState.Unconfigured => "unconfigured",
            _ => "ok",
        };

        /// <summary>Gets a value indicating whether any book is still aligning (the list is then re-read sooner).</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool HasAligning { get; init; }
    }
}
