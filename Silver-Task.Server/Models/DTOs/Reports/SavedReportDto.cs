namespace Silver_Task.Server.Models.DTOs.Reports
{
    public class SavedReportDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public Guid CreatedByUserId { get; set; }

        public required string CreatedByName { get; set; }

        public Guid? ProjectId { get; set; }

        public string? ProjectName { get; set; }

        public required string ReportType { get; set; }

        /// <summary>Raw ReportConfiguration JSON — the frontend parses this back into filter/
        /// builder state to re-populate the form; never re-interpreted server-side as anything
        /// but the closed ReportConfiguration shape (see its own doc comment).</summary>
        public required string Configuration { get; set; }

        public bool IsOwnedByMe { get; set; }

        public bool IsFavorite { get; set; }

        /// <summary>Only populated for the owner's own view of their report — a recipient sees
        /// their own access, not the full share list.</summary>
        public List<SharedUserDto>? SharedWith { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public class SharedUserDto
    {
        public Guid UserId { get; set; }

        public required string Name { get; set; }
    }

    public class SaveReportRequest
    {
        public required string Name { get; set; }

        public string? Description { get; set; }

        public Guid? ProjectId { get; set; }

        public required string Configuration { get; set; }
    }

    public class ShareReportRequest
    {
        public required string Email { get; set; }
    }
}
