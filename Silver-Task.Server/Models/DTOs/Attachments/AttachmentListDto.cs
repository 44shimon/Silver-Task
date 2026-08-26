namespace Silver_Task.Server.Models.DTOs.Attachments
{
    /// <summary>Same shape/rationale as NotificationListDto — Project Files is this app's other
    /// genuinely paginated list (a project's file count has no natural cap the way a task's
    /// attachment count does).</summary>
    public class AttachmentListDto
    {
        public required List<AttachmentDto> Items { get; set; }

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}
