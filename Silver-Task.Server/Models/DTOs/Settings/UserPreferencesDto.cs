namespace Silver_Task.Server.Models.DTOs.Settings
{
    public class UserPreferencesDto
    {
        public string Theme { get; set; } = "System";

        public Guid? DefaultProjectId { get; set; }

        public string? DefaultTaskView { get; set; }

        public string DateFormat { get; set; } = "MM/dd/yyyy";

        public string TimeFormat { get; set; } = "12h";

        public string TimeZone { get; set; } = "UTC";

        public int ItemsPerPage { get; set; } = 25;

        /// <summary>Phase 45 — master switch, checked before any per-type email preference.</summary>
        public bool EmailNotificationsEnabled { get; set; } = true;

        public bool QuietHoursEnabled { get; set; }

        public TimeOnly? QuietHoursStart { get; set; }

        public TimeOnly? QuietHoursEnd { get; set; }

        /// <summary>Phase 46 — local time (this user's TimeZone above) their Daily Digest sends.</summary>
        public TimeOnly DailyDigestTime { get; set; } = new(8, 0);

        /// <summary>Phase 46 — a System.DayOfWeek name, e.g. "Monday".</summary>
        public string WeeklyDigestDay { get; set; } = "Monday";

        public TimeOnly WeeklyDigestTime { get; set; } = new(8, 0);

        public string DefaultLandingPage { get; set; } = "Dashboard";

        /// <summary>Raw JSON string (widget visibility + order) — the frontend owns parsing/
        /// shaping this, same "server just stores an opaque blob" treatment as
        /// Notification.Metadata. Null means "no customization saved yet, use the client's
        /// built-in default layout".</summary>
        public string? DashboardLayout { get; set; }
    }
}
