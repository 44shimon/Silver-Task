using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.V1
{
    public static class TaskV1MappingExtensions
    {
        /// <summary>Assumes AssignedTo is loaded when non-null — true for every TaskItem instance
        /// ITaskService ever returns (GetAllForProjectAsync/LoadTaskAsync both .Include it; see
        /// TaskService.cs), the same assumption the internal TaskMappingExtensions.ToDto already
        /// relies on.</summary>
        public static TaskV1Dto ToV1Dto(this TaskItem task) => new()
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            AssignedTo = task.AssignedTo?.ToSummaryDto(),
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            CompletedAt = task.CompletedAt,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };
    }
}
