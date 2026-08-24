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
    }
}
