namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Per-user favorite marker for a SavedView (Phase 43) — same shape as
    /// UserReportFavorite/UserTemplateFavorite. Deliberately NO database-level foreign key onto
    /// SavedViewId (see SavedViewShareConfiguration's sibling config for the FK it does have on
    /// Shares) — a favorite can point at one of the six virtual system-default views (well-known,
    /// non-persisted GUIDs; see SavedViewService's own doc comment), which have no real SavedView
    /// row to reference. Validity (view exists or is a recognized system-default id) is checked at
    /// the application layer in SavedViewService.FavoriteAsync instead.</summary>
    public class UserSavedViewFavorite
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public Guid SavedViewId { get; set; }

        /// <summary>Drives the drag-drop favorite ordering (spec #54) — a plain sortable integer,
        /// same fractional-index-free approach as everywhere else a short, user-owned list needs
        /// manual ordering without renumbering complexity at this scale (a user's favorites list is
        /// always small).</summary>
        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }

        // Deliberately no SavedView navigation property — EF Core's convention-based discovery
        // would otherwise infer a real FK constraint from a SavedViewId-shaped property + a
        // matching navigation, which would reject favoriting a system-default virtual view (no
        // real SavedView row exists for those well-known ids). See this class's own doc comment.
    }
}
