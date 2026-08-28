using Silver_Task.Server.Common;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>One task definition within a ProjectTemplate — self-referencing via
    /// ParentTemplateTaskId for subtasks, same shape/nesting-limit convention as
    /// TaskItem.ParentTaskId. Title/Description may contain the fixed {{ProjectName}}/
    /// {{StartDate}}/{{ProjectManager}} tokens (see TemplateVariables), substituted at
    /// instantiation time only — never interpreted as executable code.
    ///
    /// StartOffsetDays/DueOffsetDays are calendar-day offsets from the new project's Start Date
    /// (this app has no business-day calendar engine anywhere to reuse — see the Phase 40 final
    /// report's disclosed limitation). EstimatedDurationDays is a template-only concept used to
    /// derive DueOffsetDays when one isn't set explicitly; it is never written back onto the real
    /// TaskItem (which has no duration field — see TaskItem's own field list).</summary>
    public class ProjectTemplateTask
    {
        public Guid Id { get; set; }

        public Guid ProjectTemplateId { get; set; }

        public Guid? ParentTemplateTaskId { get; set; }

        public required string Title { get; set; }

        public string? Description { get; set; }

        public TaskItemStatus Status { get; set; } = TaskItemStatus.NotStarted;

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        public int? StartOffsetDays { get; set; }

        public int? DueOffsetDays { get; set; }

        public int? EstimatedDurationDays { get; set; }

        /// <summary>One of TemplateAssignmentModes.All.</summary>
        public string AssignmentMode { get; set; } = TemplateAssignmentModes.Unassigned;

        /// <summary>Only meaningful when AssignmentMode == SpecificUser.</summary>
        public Guid? AssignedToUserId { get; set; }

        public double SortOrder { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ProjectTemplate? ProjectTemplate { get; set; }

        public ProjectTemplateTask? ParentTemplateTask { get; set; }

        public ICollection<ProjectTemplateTask> Subtasks { get; set; } = [];

        public User? AssignedTo { get; set; }

        public ICollection<ProjectTemplateTaskTag> Tags { get; set; } = [];

        public ICollection<ProjectTemplateTaskCustomValue> CustomValues { get; set; } = [];

        public ICollection<ProjectTemplateTaskChecklistItem> ChecklistItems { get; set; } = [];
    }
}
