namespace Silver_Task.Server.Models.Entities
{
    /// <summary>One row per user (lazily created on first access — see
    /// UserPreferencesService.GetOrCreateAsync), not an EAV table, since this is a small fixed
    /// set of fields defined by the product spec rather than something that needs to grow
    /// without a migration (unlike UserNotificationSetting).</summary>
    public class UserPreference
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string Theme { get; set; } = "System";

        public Guid? DefaultProjectId { get; set; }

        /// <summary>One of the five project view ids ("table"/"kanban"/"calendar"/"timeline"/
        /// "gantt"), or null for "no preference — use Table". Kept as free text (not an enum)
        /// so it stays a plain string match against ProjectViewTabs' own ViewId union, the same
        /// value already carried in the ?view= URL param.</summary>
        public string? DefaultTaskView { get; set; }

        public string DateFormat { get; set; } = "MM/dd/yyyy";

        public string TimeFormat { get; set; } = "12h";

        public string TimeZone { get; set; } = "UTC";

        public int ItemsPerPage { get; set; } = 25;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User? User { get; set; }

        public Project? DefaultProject { get; set; }
    }
}
