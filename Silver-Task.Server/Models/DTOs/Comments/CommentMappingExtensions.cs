using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Comments
{
    public static class CommentMappingExtensions
    {
        public static CommentDto ToDto(this TaskComment comment) => new()
        {
            Id = comment.Id,
            TaskId = comment.TaskId,
            User = comment.User!.ToSummaryDto(),
            Text = comment.Text,
            IsAutomated = comment.IsAutomated,
            AutomationId = comment.AutomationId,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt
        };
    }
}
