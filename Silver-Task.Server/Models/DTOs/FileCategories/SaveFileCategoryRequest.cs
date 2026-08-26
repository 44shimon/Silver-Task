using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.FileCategories
{
    /// <summary>Admin-only — used for both create and rename/redescribe.</summary>
    public class SaveFileCategoryRequest
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public required string Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }
    }
}
