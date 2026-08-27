namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Many-to-many TaskItem &lt;-&gt; Tag link (Phase 35) — reuses the exact same global
    /// Tag vocabulary Phase 34 introduced for files, rather than inventing a second "task label"
    /// concept; mirrors FileTag's shape exactly. Exists specifically so "Labels" can be a real,
    /// user-visible/editable task field (Task Detail's own Labels section) and not just an
    /// automation-only concept with nothing to point at.</summary>
    public class TaskTag
    {
        public Guid Id { get; set; }

        public Guid TaskId { get; set; }

        public Guid TagId { get; set; }

        public DateTime CreatedAt { get; set; }

        public TaskItem? Task { get; set; }

        public Tag? Tag { get; set; }
    }
}
