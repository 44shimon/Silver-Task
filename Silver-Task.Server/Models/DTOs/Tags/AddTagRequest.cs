using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Tags
{
    public class AddTagRequest
    {
        [Required, StringLength(50, MinimumLength = 1)]
        public required string Name { get; set; }
    }
}
