namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Explicit user-to-user share of a SavedReport (Phase 38) — deliberately narrow
    /// (no project-members-bulk or role-bulk sharing) since the report's own security boundary
    /// doesn't depend on how it was shared: a shared report with a ProjectId set still re-checks
    /// the executing user's LIVE project access every time it runs (see SavedReport's doc comment).
    /// A row here only ever grants "this report appears in your list" — never grants extra data
    /// access beyond what the viewer could already see.</summary>
    public class SavedReportShare
    {
        public Guid Id { get; set; }

        public Guid SavedReportId { get; set; }

        public Guid SharedWithUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public SavedReport? SavedReport { get; set; }

        public User? SharedWithUser { get; set; }
    }
}
