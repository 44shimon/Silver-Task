using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Notifications
{
    public static class NotificationMappingExtensions
    {
        public static NotificationDto ToDto(this Notification notification) => new()
        {
            Id = notification.Id,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            TaskId = notification.TaskId,
            ProjectId = notification.ProjectId,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt
        };
    }
}
