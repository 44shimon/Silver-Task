namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Phase 40 — a reusable blueprint for creating a whole project's task structure at
    /// once. Editing a template never touches projects already created from it — instantiation
    /// COPIES data into independent Project/TaskItem rows (see ITemplateInstantiationService),
    /// there is no live reference from a created project back to this template that changes could
    /// propagate through. UsageCount/LastUsedAt are denormalized onto the template itself, same
    /// "avoid a per-use aggregate query" precedent as Automation.RunCount/LastRunAt (Phase 35).</summary>
    public class ProjectTemplate
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public Guid CreatedByUserId { get; set; }

        /// <summary>Phase 40 visibility (spec #37) — this single-tenant app has no Project/Team or
        /// Organization concept to hang a three-tier visibility model on (see the Phase 40 final
        /// report's own research note), so this collapses to two honest tiers: Private (owner +
        /// explicit TemplateShare rows only) and Public (every authenticated user with
        /// Permissions.TemplatesView/Use can see and use it — the closest real equivalent of
        /// "Organization" when there is only one organization). Defaults to false (private).</summary>
        public bool IsPublic { get; set; }

        /// <summary>Archived templates can't normally be used but remain visible for history —
        /// same convention as Project.IsArchived, not the User/Attachment soft-delete shape (see
        /// this entity's own Phase 40 doc comment reasoning in the final report).</summary>
        public bool IsArchived { get; set; }

        public DateTime? ArchivedAt { get; set; }

        public int UsageCount { get; set; }

        public DateTime? LastUsedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User? CreatedBy { get; set; }

        public ICollection<ProjectTemplateTask> Tasks { get; set; } = [];

        public ICollection<ProjectTemplateTaskDependency> Dependencies { get; set; } = [];

        public ICollection<TemplateShare> Shares { get; set; } = [];

        public ICollection<UserTemplateFavorite> FavoritedBy { get; set; } = [];
    }
}
