namespace Silver_Task.Server.Models.DTOs.Search
{
    /// <summary>Phase 42 — one row in a global search result set, regardless of entity Type.
    /// Deliberately a single flat shape (matching the spec's own suggested response shape,
    /// section 79) rather than a type-per-entity DTO hierarchy — every field beyond
    /// Type/Id/Title/Score is optional and simply left null for types it doesn't apply to, the
    /// same "denormalized display row" convention already used by report DTOs like
    /// OverdueTaskRow/BlockedTaskRow. ActionUrl is computed server-side, the same
    /// NotificationService.DefaultActionUrl convention this app already established, so the
    /// frontend never needs its own per-type routing table.</summary>
    public class SearchResultDto
    {
        public required string Type { get; set; }

        public Guid Id { get; set; }

        public required string Title { get; set; }

        /// <summary>A short, human-readable reason this result matched — e.g. "Permit Number:
        /// BP-2026-1034" for a custom-field match, or a comment excerpt. Null when the title/
        /// name match is self-explanatory.</summary>
        public string? Snippet { get; set; }

        public required string ActionUrl { get; set; }

        /// <summary>Relevance score (spec #15's own ranking order, translated to a numeric
        /// weight) — higher is more relevant. Only meaningful for sort=relevance.</summary>
        public double Score { get; set; }

        public Guid? ProjectId { get; set; }

        public string? ProjectName { get; set; }

        public string? Status { get; set; }

        public string? Priority { get; set; }

        public string? AssigneeName { get; set; }

        public DateOnly? DueDate { get; set; }

        public List<string>? TagNames { get; set; }

        public long? FileSizeBytes { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Per-type result counts — backs the search page's entity-type tabs (spec #21/#22)
    /// and the Topbar dropdown's group headers, computed from the same query that produced
    /// Results rather than a second round of COUNT queries per type.</summary>
    public class SearchCountsDto
    {
        public int Tasks { get; set; }

        public int Projects { get; set; }

        public int Users { get; set; }

        public int Files { get; set; }

        public int Comments { get; set; }

        public int Tags { get; set; }

        public int Templates { get; set; }
    }

    public class SearchResponseDto
    {
        public required string Query { get; set; }

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        public required SearchCountsDto Counts { get; set; }

        public required List<SearchResultDto> Results { get; set; }
    }
}
