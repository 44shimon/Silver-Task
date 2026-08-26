using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Tags
{
    /// <summary>Admin-only (Admin -> Tags) — renaming the shared global definition.</summary>
    public class UpdateTagRequest
    {
        [Required, StringLength(50, MinimumLength = 1)]
        public required string Name { get; set; }
    }
}
