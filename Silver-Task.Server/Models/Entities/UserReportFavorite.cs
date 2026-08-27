namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Per-user favorite marker for a SavedReport (Phase 38) — same shape/reasoning as
    /// UserFileFavorite: favorite status is per-(user, report), not a property of the report
    /// itself.</summary>
    public class UserReportFavorite
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public Guid SavedReportId { get; set; }

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }

        public SavedReport? SavedReport { get; set; }
    }
}
