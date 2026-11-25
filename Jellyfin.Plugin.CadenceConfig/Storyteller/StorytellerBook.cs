using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>A book as listed by Storyteller's <c>GET /api/v2/books</c> (the fields we read).</summary>
    public sealed class StorytellerBook
    {
        /// <summary>Gets or sets the book uuid.</summary>
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = string.Empty;

        /// <summary>Gets or sets the title.</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>Gets or sets the authors.</summary>
        [JsonPropertyName("authors")]
        public IReadOnlyList<StorytellerCreator> Authors { get; set; } = Array.Empty<StorytellerCreator>();

        /// <summary>Gets or sets the audiobook side (its folder carries the audiobook's own title), may be null.</summary>
        [JsonPropertyName("audiobook")]
        public StorytellerAudiobook? Audiobook { get; set; }

        /// <summary>Gets or sets when alignment finished (null while queued/aligning).</summary>
        [JsonPropertyName("alignedAt")]
        public string? AlignedAt { get; set; }

        /// <summary>Gets or sets the last-updated stamp (part of the timeline cache key).</summary>
        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }

        /// <summary>Gets a fingerprint that changes whenever the aligned output may have changed.</summary>
        [JsonIgnore]
        public string Fingerprint => $"{AlignedAt}|{UpdatedAt}";
    }
}
