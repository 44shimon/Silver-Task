using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Folders
{
    public class CreateFolderRequest
    {
        [Required, StringLength(255, MinimumLength = 1)]
        public required string Name { get; set; }

        public Guid? ParentFolderId { get; set; }
    }
}
