namespace Silver_Task.Server.Models.DTOs.Folders
{
    /// <summary>Backs the "This folder contains N files and M subfolders" confirmation shown
    /// before an actual delete.</summary>
    public class FolderDeletePreviewDto
    {
        public int FileCount { get; set; }

        public int SubfolderCount { get; set; }
    }
}
