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

        public string DigestFrequency { get; set; } = "Immediately";

        public bool QuietHoursEnabled { get; set; }

        public TimeOnly? QuietHoursStart { get; set; }

        public TimeOnly? QuietHoursEnd { get; set; }
    }
}
