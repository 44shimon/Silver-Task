using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Common.Automation
{
    /// <summary>The recognized condition field keys (Phase 35) — plain string constants (like
    /// NotificationTypes), not an enum, because the valid set differs per TriggerType and spans
    /// several unrelated entity "namespaces" (Task/File/Project); "Task.CustomField:{fieldId}" is
    /// the one dynamic/open-ended exception, since a project's own custom fields aren't known
    /// ahead of time. See GetApplicableFields for which of these apply to a given trigger, used by
    /// both the builder UI (what to offer) and AutomationValidator (what to reject).</summary>
    public static class AutomationFields
    {
        public const string TaskTitle = "Task.Title";
        public const string TaskDescription = "Task.Description";
        public const string TaskStatus = "Task.Status";
        public const string TaskPriority = "Task.Priority";
        public const string TaskAssigneeId = "Task.AssigneeId";
        public const string TaskCreatorId = "Task.CreatorId";
        public const string TaskDueDate = "Task.DueDate";
        public const string TaskStartDate = "Task.StartDate";
        public const string TaskProjectId = "Task.ProjectId";
        public const string TaskParentTaskId = "Task.ParentTaskId";
        public const string TaskLabels = "Task.Labels";
        public const string TaskCustomFieldPrefix = "Task.CustomField:";

        /// <summary>Only meaningful for the SubtaskCompleted trigger — "true"/"false" against
        /// Equals, computed at evaluation time (are every one of the parent's direct children
        /// Status == Complete right now). See AutomationConditionEvaluator's own doc comment.</summary>
        public const string TaskAllSiblingSubtasksComplete = "Task.AllSiblingSubtasksComplete";

        public const string FileFileName = "File.FileName";
        public const string FileCategoryId = "File.CategoryId";
        public const string FileTags = "File.Tags";
        public const string FileType = "File.FileType";
        public const string FileUploadedByUserId = "File.UploadedByUserId";
        public const string FileProjectId = "File.ProjectId";
        public const string FileTaskId = "File.TaskId";

        public const string ProjectName = "Project.Name";
        public const string ProjectStatus = "Project.Status";
        public const string ProjectOwnerId = "Project.OwnerId";

        private static readonly IReadOnlyList<string> TaskFields =
        [
            TaskTitle, TaskDescription, TaskStatus, TaskPriority, TaskAssigneeId, TaskCreatorId,
            TaskDueDate, TaskStartDate, TaskProjectId, TaskParentTaskId, TaskLabels
        ];

        private static readonly IReadOnlyList<string> FileFields =
        [
            FileFileName, FileCategoryId, FileTags, FileType, FileUploadedByUserId, FileProjectId, FileTaskId
        ];

        private static readonly IReadOnlyList<string> ProjectFields = [ProjectName, ProjectStatus, ProjectOwnerId];

        /// <summary>The non-dynamic fields a builder UI should offer for a given trigger — custom
        /// field conditions ("Task.CustomField:{id}") are added separately by the caller once it
        /// knows the project's own custom field list, since this method has no project context.</summary>
        public static IReadOnlyList<string> GetApplicableFields(AutomationTriggerType triggerType) => triggerType switch
        {
            AutomationTriggerType.FileUploaded or AutomationTriggerType.FileTagged => FileFields,
            AutomationTriggerType.ProjectCreated => ProjectFields,
            AutomationTriggerType.SubtaskCompleted => [.. TaskFields, TaskAllSiblingSubtasksComplete],
            _ => TaskFields
        };

        public static bool IsValidField(AutomationTriggerType triggerType, string field) =>
            field.StartsWith(TaskCustomFieldPrefix, StringComparison.Ordinal) || GetApplicableFields(triggerType).Contains(field);
    }
}
