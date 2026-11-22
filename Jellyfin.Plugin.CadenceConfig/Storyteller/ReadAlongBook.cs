using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The wire response for <c>GET /Cadence/Storyteller/Books/{itemId}</c>.</summary>
    public sealed class ReadAlongBook
    {
        /// <summary>Gets or sets the status (its wire form, lowercase, is <see cref="Status"/>).</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public ReadAlongStatus Kind { get; set; }

        /// <summary>Gets the status as the client expects it: unlinked | unmatched | aligning | mismatch | ready.</summary>
        public string Status => ToWire(Kind);

        /// <summary>Gets or sets the matched Storyteller book uuid.</summary>
        public string? BookUuid { get; set; }

        /// <summary>Gets or sets the matched Storyteller title.</summary>
        public string? Title { get; set; }

        /// <summary>Gets or sets the whole book's duration in seconds (sum of Jellyfin parts).</summary>
        public double Duration { get; set; }

        /// <summary>Gets or sets the Jellyfin parts in play order.</summary>
        public IReadOnlyList<ReadAlongPart> Parts { get; set; } = Array.Empty<ReadAlongPart>();

        /// <summary>Gets or sets the chapters.</summary>
        public IReadOnlyList<ReadAlongChapter> Chapters { get; set; } = Array.Empty<ReadAlongChapter>();

        /// <summary>Gets or sets the clips, sorted by start. Empty unless <see cref="ReadAlongStatus.Ready"/>.</summary>
        public IReadOnlyList<ReadAlongClip> Clips { get; set; } = Array.Empty<ReadAlongClip>();

        /// <summary>The lowercase wire name of a status.</summary>
        /// <param name="status">The status.</param>
        /// <returns>The wire string.</returns>
        public static string ToWire(ReadAlongStatus status)
        {
            return status switch
            {
                ReadAlongStatus.Unlinked => "unlinked",
                ReadAlongStatus.Unmatched => "unmatched",
                ReadAlongStatus.Aligning => "aligning",
                ReadAlongStatus.Mismatch => "mismatch",
                ReadAlongStatus.Empty => "empty",
                _ => "ready",
            };
        }
    }
}
