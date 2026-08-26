namespace Silver_Task.Server.Models.DTOs.Folders
{
    public class MoveFolderRequest
    {
        /// <summary>Null moves the folder to the project's top level.</summary>
        public Guid? ParentFolderId { get; set; }
    }
}
