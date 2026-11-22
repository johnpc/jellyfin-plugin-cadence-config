using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The aligned book flattened onto one global audio timeline.</summary>
    public sealed class Timeline
    {
        /// <summary>Gets or sets the Storyteller book uuid.</summary>
        public string BookUuid { get; set; } = string.Empty;

        /// <summary>Gets or sets the fingerprint the timeline was built from (cache key).</summary>
        public string Fingerprint { get; set; } = string.Empty;

        /// <summary>Gets or sets the chapters in reading order.</summary>
        public IReadOnlyList<ReadAlongChapter> Chapters { get; set; } = Array.Empty<ReadAlongChapter>();

        /// <summary>Gets or sets the audio chunk paths in timeline order (root-relative).</summary>
        public IReadOnlyList<string> AudioFiles { get; set; } = Array.Empty<string>();

        /// <summary>Gets or sets each chunk's start on the global timeline, parallel to <see cref="AudioFiles"/>.</summary>
        public IReadOnlyList<double> AudioOffsets { get; set; } = Array.Empty<double>();

        /// <summary>Gets or sets the total aligned audio length (sum of chunk durations).</summary>
        public double Total { get; set; }

        /// <summary>Gets or sets the clips sorted by global start.</summary>
        public IReadOnlyList<ReadAlongClip> Clips { get; set; } = Array.Empty<ReadAlongClip>();
    }
}
