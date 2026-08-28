namespace Silver_Task.Server.Models.DTOs.SavedViews
{
    public class SavedViewDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public Guid CreatedByUserId { get; set; }

        public required string CreatedByName { get; set; }

        public required string EntityType { get; set; }

        public bool IsPublic { get; set; }

        public required SavedViewFilterGroupDto Filter { get; set; }

        public List<string> Columns { get; set; } = [];

        public string? SortField { get; set; }

        public bool SortDescending { get; set; }

        public string? GroupByField { get; set; }

        public required string Layout { get; set; }

        public bool IsOwnedByMe { get; set; }

        public bool IsFavorite { get; set; }

        /// <summary>Where this favorite sits in the caller's own favorites ordering — only
        /// meaningful when IsFavorite is true. See UserSavedViewFavorite.SortOrder.</summary>
        public int? FavoriteSortOrder { get; set; }

        /// <summary>True for one of the six virtual, non-persisted views (My Tasks/Overdue/Due
        /// Today/Due This Week/Recently Updated/Unassigned) — see SavedViewService's own doc
        /// comment. A system default can be favorited/opened but never edited/deleted/shared.</summary>
        public bool IsSystemDefault { get; set; }

        /// <summary>Only populated for the owner's own view of their view — a recipient sees their
        /// own access, not the full share list (same convention as SavedReportDto).</summary>
        public List<SavedViewSharedUserDto>? SharedWith { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public class SavedViewSharedUserDto
    {
        public Guid UserId { get; set; }

        public required string Name { get; set; }
    }

    public class SaveViewRequest
    {
        public required string Name { get; set; }

        public string? Description { get; set; }

        public required string EntityType { get; set; }

        public bool IsPublic { get; set; }

        public required SavedViewFilterGroupDto Filter { get; set; }

        public List<string>? Columns { get; set; }

        public string? SortField { get; set; }

        public bool SortDescending { get; set; }

        public string? GroupByField { get; set; }

        public string Layout { get; set; } = "Table";
    }

    public class ShareViewRequest
    {
        public required string Email { get; set; }
    }

    /// <summary>One page of a view's execution results (spec's own explicit "filtering must occur
    /// server-side, must remain responsive at scale" requirement) — Tasks/Projects is populated
    /// per the view's EntityType, never both.</summary>
    public class ExecuteViewResultDto
    {
        public List<Models.DTOs.Tasks.TaskDto> Tasks { get; set; } = [];

        public List<Models.DTOs.Projects.ProjectDto> Projects { get; set; } = [];

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        /// <summary>Set only when every matched row belongs to exactly one project — lets the
        /// frontend legitimately reuse the existing single-project Kanban/Calendar/Timeline/Gantt
        /// components for a view that happens to resolve to one project, per the spec's own
        /// "do not rebuild these views" instruction. Null (never assume) for a multi-project match
        /// set, which can only render as Table.</summary>
        public Guid? ResolvedSingleProjectId { get; set; }

        /// <summary>Filter fields that no longer resolve (a deleted/archived custom field, a
        /// deleted tag) — surfaced so the frontend can render "Filter unavailable: X" and offer to
        /// remove just that condition, instead of crashing or silently misbehaving (spec's own
        /// graceful-degradation requirement).</summary>
        public List<string> UnavailableFilterFields { get; set; } = [];
    }

    /// <summary>Ad-hoc preview execution (spec's own "N matching tasks" live count) — same shape
    /// as SaveViewRequest's filter portion, but never persisted; used while building/editing a
    /// view before the user commits Save.</summary>
    public class PreviewViewRequest
    {
        public required string EntityType { get; set; }

        public required SavedViewFilterGroupDto Filter { get; set; }
    }

    /// <summary>The lightweight "N matching tasks" count (spec's own explicit "must not run on
    /// every keystroke, must not require rendering the full result set" requirement) — the
    /// frontend debounces calls to this, never fires it inline with every filter-builder edit.</summary>
    public class PreviewResultDto
    {
        public int Total { get; set; }

        public Guid? ResolvedSingleProjectId { get; set; }

        public List<string> UnavailableFilterFields { get; set; } = [];
    }
}
