using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Attachments
{
    public class UpdateDescriptionRequest
    {
        [StringLength(2000)]
        public string? Description { get; set; }
    }
}
