namespace Silver_Task.Server.Models.DTOs.Email
{
    /// <summary>Admin delivery log row — deliberately excludes the email body/HTML and the
    /// recipient's raw email address (RecipientUserId only) per spec §69's "do not expose email
    /// contents or recipient information unnecessarily".</summary>
    public class EmailDeliveryDto
    {
        public Guid Id { get; set; }

        public required string NotificationType { get; set; }

        public Guid RecipientUserId { get; set; }

        public string? RecipientName { get; set; }

        public required string Status { get; set; }

        public int AttemptCount { get; set; }

        public string? LastError { get; set; }

        public DateTime QueuedAt { get; set; }

        public DateTime? SentAt { get; set; }

        public DateTime? FailedAt { get; set; }
    }

    public class EmailDeliveryPageDto
    {
        public required IReadOnlyList<EmailDeliveryDto> Items { get; set; }

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}
