using Silver_Task.Server.Common;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Phase 40 — a reusable definition for a SINGLE task, used to create one task in an
    /// EXISTING project the caller picks at use time (unlike ProjectTemplate, which creates a
    /// whole new project). Name doubles as the resulting task's Title (may contain the fixed
    /// {{ProjectName}}/{{StartDate}}/{{ProjectManager}} tokens — see TemplateVariables).
    /// Deliberately much lighter than ProjectTemplate: no dependency graph, since a single
    /// template task has nothing else in the same instantiation to depend on.</summary>
    public class TaskTemplate
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public TaskItemStatus Status { get; set; } = TaskItemStatus.NotStarted;

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        /// <summary>Offsets are relative to "today" (in the target project's caller's own
        /// timezone) at use time — there is no project start date to anchor to for a standalone
        /// task template.</summary>
        public int? StartOffsetDays { get; set; }

        public int? DueOffsetDays { get; set; }

        public int? EstimatedDurationDays { get; set; }

        public string AssignmentMode { get; set; } = TemplateAssignmentModes.Unassigned;

        public Guid? AssignedToUserId { get; set; }

        public Guid CreatedByUserId { get; set; }

        /// <summary>See ProjectTemplate.IsPublic's own doc comment for the two-tier
        /// Private/Public visibility model this app uses instead of a three-tier one.</summary>
        public bool IsPublic { get; set; }

        public bool IsArchived { get; set; }

        public DateTime? ArchivedAt { get; set; }

        public int UsageCount { get; set; }

        public DateTime? LastUsedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User? CreatedBy { get; set; }

        public User? AssignedTo { get; set; }

        public ICollection<TaskTemplateTag> Tags { get; set; } = [];

        public ICollection<TaskTemplateCustomValue> CustomValues { get; set; } = [];

        public ICollection<TaskTemplateChecklistItem> ChecklistItems { get; set; } = [];

        public ICollection<TemplateShare> Shares { get; set; } = [];

        public ICollection<UserTemplateFavorite> FavoritedBy { get; set; } = [];
    }
}
