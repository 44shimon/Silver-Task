namespace Silver_Task.Server.Models.Entities
{
    /// <summary>A saved, reusable filter/column/sort/layout configuration over Tasks or Projects
    /// (Phase 43). Two-tier Private/Public visibility, same model as ProjectTemplate/TaskTemplate/
    /// SavedReport (this single-tenant app has no Team/Organization concept — confirmed across
    /// Phases 40-42) — IsPublic plus explicit user-to-user Shares (SavedViewShare) covers every
    /// visibility tier the spec asks for that this app's permission model actually supports.
    /// FilterJson/Columns are validated, closed-shape JSON (never raw/executable) — see
    /// SavedViewFilterValidator. A SavedView never widens access on its own: every execution
    /// re-checks the CURRENT caller's live project access (ISavedViewExecutionService), regardless
    /// of who created or shared the view or what access existed at share/save time — same rule as
    /// SavedReport.PrepareExecutionAsync.</summary>
    public class SavedView
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public Guid CreatedByUserId { get; set; }

        /// <summary>SavedViewEntityTypes.Task or .Project — immutable after creation (same
        /// convention as CustomField.FieldType).</summary>
        public required string EntityType { get; set; }

        public bool IsPublic { get; set; }

        /// <summary>Serialized SavedViewFilterGroupDto — a validated, recursive AND/OR filter
        /// tree. Never blindly executed as client-constructed SQL: every leaf condition is
        /// resolved into one targeted, parameterized query by SavedViewFilterEngine.</summary>
        public required string FilterJson { get; set; }

        /// <summary>JSON array of built-in/custom-field column keys to show, in order. Null/empty
        /// means "use the default column set" for the view's EntityType.</summary>
        public string? Columns { get; set; }

        public string? SortField { get; set; }

        public bool SortDescending { get; set; }

        /// <summary>Built-in field key or "customField:{guid}" to group rows by — grouping is a
        /// lightweight client-side re-bucketing of the already-fetched page, not a server-side
        /// GROUP BY (see SavedViewPage's own doc comment).</summary>
        public string? GroupByField { get; set; }

        /// <summary>SavedViewLayouts constant. Only meaningful for Task-entity views — Project-
        /// entity views always render as Table.</summary>
        public string Layout { get; set; } = "Table";

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User? CreatedBy { get; set; }

        public ICollection<SavedViewShare> Shares { get; set; } = [];

        // Deliberately no FavoritedBy collection navigation — see UserSavedViewFavorite's own doc
        // comment on why that relationship is intentionally unconstrained at the database level
        // (a favorite can point at a virtual system-default view with no real row here). Favorite
        // state is always queried directly against UserSavedViewFavorites instead.
    }
}
