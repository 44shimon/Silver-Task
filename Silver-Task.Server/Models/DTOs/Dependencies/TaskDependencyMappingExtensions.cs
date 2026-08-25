using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Dependencies
{
    public static class TaskDependencyMappingExtensions
    {
        /// <summary>For the "Depends On" list — describes the prerequisite (DependsOnTask).</summary>
        public static TaskDependencyDto ToDependsOnDto(this TaskDependency dependency)
        {
            var prerequisite = dependency.DependsOnTask!;
            return new TaskDependencyDto
            {
                DependencyId = dependency.Id,
                DependencyType = dependency.DependencyType,
                CreatedAt = dependency.CreatedAt,
                TaskId = prerequisite.Id,
                Title = prerequisite.Title,
                Status = prerequisite.Status,
                Priority = prerequisite.Priority,
                AssignedTo = prerequisite.AssignedTo?.ToSummaryDto(),
                DueDate = prerequisite.DueDate
            };
        }

        /// <summary>For the "Blocking" list — describes the dependent (Task).</summary>
        public static TaskDependencyDto ToDependentDto(this TaskDependency dependency)
        {
            var dependent = dependency.Task!;
            return new TaskDependencyDto
            {
                DependencyId = dependency.Id,
                DependencyType = dependency.DependencyType,
                CreatedAt = dependency.CreatedAt,
                TaskId = dependent.Id,
                Title = dependent.Title,
                Status = dependent.Status,
                Priority = dependent.Priority,
                AssignedTo = dependent.AssignedTo?.ToSummaryDto(),
                DueDate = dependent.DueDate
            };
        }
    }
}
