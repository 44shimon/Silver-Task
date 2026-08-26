namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Many-to-many Attachment &lt;-&gt; Tag link (Phase 34) — "FileId" per the spec's own
    /// naming, referencing Attachment.Id (an attachment *is* "the file" everywhere else in this
    /// codebase; there's no separate File entity to point at).</summary>
    public class FileTag
    {
        public Guid Id { get; set; }

        public Guid FileId { get; set; }

        public Guid TagId { get; set; }

        public DateTime CreatedAt { get; set; }

        public Attachment? File { get; set; }

        public Tag? Tag { get; set; }
    }
}
