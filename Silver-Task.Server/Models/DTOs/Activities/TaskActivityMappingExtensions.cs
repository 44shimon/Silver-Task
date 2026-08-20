using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Activities
{
    public static class TaskActivityMappingExtensions
    {
        public static TaskActivityDto ToDto(this TaskActivity activity) => new()
        {
            Id = activity.Id,
            User = activity.User?.ToSummaryDto(),
            Action = activity.Action,
            FieldName = activity.FieldName,
            OldValue = activity.OldValue,
            NewValue = activity.NewValue,
            CreatedAt = activity.CreatedAt
        };
    }
}
