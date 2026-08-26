using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Attachments
{
    public class RenameAttachmentRequest
    {
        [Required, StringLength(500, MinimumLength = 1)]
        public required string FileName { get; set; }
    }
}
