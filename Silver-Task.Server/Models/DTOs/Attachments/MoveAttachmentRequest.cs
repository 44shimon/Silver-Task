namespace Silver_Task.Server.Models.DTOs.Attachments
{
    public class MoveAttachmentRequest
    {
        /// <summary>Null moves the file back to the project's root level.</summary>
        public Guid? FolderId { get; set; }
    }
}
