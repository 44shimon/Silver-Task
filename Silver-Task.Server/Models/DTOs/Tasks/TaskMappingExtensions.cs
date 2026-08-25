using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Tasks
{
    public static class TaskMappingExtensions
    {
        public static TaskDto ToDto(this TaskItem task) => new()
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            ProjectName = task.Project?.Name,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            AssignedTo = task.AssignedTo?.ToSummaryDto(),
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            CompletedAt = task.CompletedAt,
            SortOrder = task.SortOrder,
            ParentTaskId = task.ParentTaskId,
            ParentTaskTitle = task.ParentTaskTitle,
            CustomValues = task.CustomValues
                .Select(v => new TaskCustomValueDto { CustomFieldId = v.CustomFieldId, Value = v.Value })
                .ToList(),
            DependsOnCount = task.DependsOnCount,
            BlockedByCount = task.BlockedByCount,
            DependentCount = task.DependentCount,
            SubtaskCount = task.SubtaskCount,
            CompletedSubtaskCount = task.CompletedSubtaskCount,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };
    }
}
