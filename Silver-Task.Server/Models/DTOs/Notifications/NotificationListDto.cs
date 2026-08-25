namespace Silver_Task.Server.Models.DTOs.Notifications
{
    /// <summary>The notification feed is this app's first genuinely paginated list — everywhere
    /// else deliberately stays client-side-filtered over an already-small, fully-loaded list
    /// (see CLAUDE.md), but notification history has no natural cap, so it gets real server-side
    /// paging instead.</summary>
    public class NotificationListDto
    {
        public required List<NotificationDto> Items { get; set; }

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}
