using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Grab
{
    /// <summary>A download job returned by /api/download and polled via /api/jobs/{id}. The grabber
    /// writes the file into the shared music library and rescans itself, so we only track the job
    /// STATUS — there's no file path to act on (verified against the live API). NOTE: the two
    /// endpoints name the id field differently — /api/download returns "job_id", /api/jobs/{id}
    /// returns "id" — so both are mapped and <see cref="Id"/> prefers whichever is present.</summary>
    public sealed class GrabJob
    {
        /// <summary>Gets or sets the id as returned by /api/jobs/{id} ("id").</summary>
        [JsonPropertyName("id")]
        public string? IdField { get; set; }

        /// <summary>Gets or sets the id as returned by /api/download ("job_id").</summary>
        [JsonPropertyName("job_id")]
        public string? JobIdField { get; set; }

        /// <summary>Gets the job id from whichever field the responding endpoint populated.</summary>
        [JsonIgnore]
        public string? Id => IdField ?? JobIdField;

        /// <summary>Gets or sets the job status (queued, downloading, completed, failed, …).</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
