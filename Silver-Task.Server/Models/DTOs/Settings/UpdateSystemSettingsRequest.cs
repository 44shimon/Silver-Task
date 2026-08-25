using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Settings
{
    public class UpdateSystemSettingsRequest
    {
        /// <summary>Only the keys the admin actually changed need to be present — anything
        /// omitted keeps its current value. Every key is still validated against the known
        /// definition set server-side regardless of what the UI sent.</summary>
        [Required]
        public required Dictionary<string, string> Values { get; set; }
    }
}
