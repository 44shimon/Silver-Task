namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Explicit user-to-user share of a SavedView (Phase 43) — mirrors SavedReportShare
    /// exactly. Sharing is deliberately narrow (no bulk project-members/role sharing, since this
    /// app has no Team/Organization concept to share against); the view's actual security boundary
    /// never depends on how it was shared — execution always re-verifies the CURRENT caller's live
    /// project access, same as every other project-scoped read.</summary>
    public class SavedViewShare
    {
        public Guid Id { get; set; }

        public Guid SavedViewId { get; set; }

        public Guid SharedWithUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public SavedView? SavedView { get; set; }

        public User? SharedWithUser { get; set; }
    }
}
