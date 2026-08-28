using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Search
{
    /// <summary>Phase 42 — bound from query-string parameters
    /// (GET /api/search?q=...&amp;type=...). Operator tokens embedded in Query (status:open,
    /// priority:high, assignee:john, project:"123 Main Street", tag:renovation — spec #31) are
    /// parsed out of Query and merged into these same explicit filter fields by
    /// SearchService.ParseOperators, so there is only ever one filter code path regardless of
    /// whether a filter came from the query text or a dedicated parameter.</summary>
    public class SearchRequest
    {
        public required string Query { get; set; }

        /// <summary>Null/"all" searches every supported entity type.</summary>
        public string? Type { get; set; }

        public Guid? ProjectId { get; set; }

        public TaskItemStatus? Status { get; set; }

        public TaskPriority? Priority { get; set; }

        public Guid? AssigneeId { get; set; }

        public Guid? TagId { get; set; }

        public DateOnly? DateFrom { get; set; }

        public DateOnly? DateTo { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 25;

        /// <summary>relevance (default) | newest | oldest | dueDate | updated.</summary>
        public string? Sort { get; set; }
    }
}
