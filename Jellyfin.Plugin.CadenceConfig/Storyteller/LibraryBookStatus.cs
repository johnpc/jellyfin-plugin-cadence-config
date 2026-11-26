namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>One audiobook's read-along status in <c>GET /Cadence/Storyteller/Books</c>.</summary>
    public sealed class LibraryBookStatus
    {
        /// <summary>Gets or sets the book's anchor item id (its first part — the id the client groups by).</summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>Gets or sets the status.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public ReadAlongStatus Kind { get; set; }

        /// <summary>Gets the status as the client expects it: ready | aligning | unmatched | empty | mismatch.</summary>
        public string Status => ReadAlongBook.ToWire(Kind);
    }
}
