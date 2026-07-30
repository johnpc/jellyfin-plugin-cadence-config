using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CadenceConfig.Grab
{
    /// <summary>A download job returned by /api/download and polled via /api/jobs/{id}.</summary>
    public sealed class GrabJob
    {
        /// <summary>Gets or sets the job id.</summary>
        [JsonPropertyName("job_id")]
        public string? JobId { get; set; }

        /// <summary>Gets or sets the job status (queued, downloading, completed, failed, …).</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets the absolute path of the downloaded file when completed.</summary>
        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; }
    }
}
