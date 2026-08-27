using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Settings
{
    public class UpdatePreferencesRequest
    {
        [Required]
        public required string Theme { get; set; }

        public Guid? DefaultProjectId { get; set; }

        public string? DefaultTaskView { get; set; }

        [Required]
        public required string DateFormat { get; set; }

        [Required]
        public required string TimeFormat { get; set; }

        [Required]
        public required string TimeZone { get; set; }

        [Range(5, 200)]
        public int ItemsPerPage { get; set; } = 25;

        [Required]
        public required string DigestFrequency { get; set; }

        public bool QuietHoursEnabled { get; set; }

        public TimeOnly? QuietHoursStart { get; set; }

        public TimeOnly? QuietHoursEnd { get; set; }

        [Required]
        public required string DefaultLandingPage { get; set; }

        [MaxLength(4000)]
        public string? DashboardLayout { get; set; }
    }
}
