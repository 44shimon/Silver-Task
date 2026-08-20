using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Comments
{
    public class CreateCommentRequest
    {
        [Required, StringLength(4000, MinimumLength = 1)]
        public required string Text { get; set; }
    }
}
