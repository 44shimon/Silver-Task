namespace Silver_Task.Server.Models.Entities
{
    /// <summary>A saved report configuration (Phase 38) — ProjectId null means the report is not
    /// scoped to a single project (e.g. a workload/admin report spanning everything the owner can
    /// see); when set, executing the report always re-checks the CURRENT caller's live access to
    /// that project (via ProjectAccessService, the same predicate every other project-scoped query
    /// uses), regardless of who created or shared the report — see SavedReportShare's doc comment
    /// and ISavedReportService's ExecuteAsync for where this is enforced. Configuration is a JSON
    /// blob of closed-enum filter/grouping values only (report type, date range key, project/user/
    /// status/priority/label filter ids, group-by field) — never executable code, matching the
    /// existing UserPreference.DashboardLayout/Notification.Metadata "opaque but validated JSON"
    /// pattern.</summary>
    public class SavedReport
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public Guid CreatedByUserId { get; set; }

        public Guid? ProjectId { get; set; }

        public required string Configuration { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User? CreatedBy { get; set; }

        public Project? Project { get; set; }

        public ICollection<SavedReportShare> Shares { get; set; } = [];

        public ICollection<UserReportFavorite> FavoritedBy { get; set; } = [];
    }
}
