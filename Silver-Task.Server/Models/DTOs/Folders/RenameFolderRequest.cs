using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Folders
{
    public class RenameFolderRequest
    {
        [Required, StringLength(255, MinimumLength = 1)]
        public required string Name { get; set; }
    }
}
