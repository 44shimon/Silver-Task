using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Tasks
{
    public class CreateTaskRequest
    {
        [Required, StringLength(500, MinimumLength = 1)]
        public required string Title { get; set; }

        [StringLength(10000)]
        public string? Description { get; set; }

        /// <summary>Null means "use the configured default" (see
        /// TaskService.ResolveDefaultStatusAsync / SystemSettingKeys.DefaultTaskStatus) — kept
        /// nullable specifically so the service layer can distinguish "omitted" from "explicitly
        /// chosen this value", which a non-nullable property with a hardcoded default couldn't.</summary>
        public TaskItemStatus? Status { get; set; }

        public TaskPriority? Priority { get; set; }

        public Guid? AssignedToUserId { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? DueDate { get; set; }
    }
}
