using Silver_Task.Server.Models.DTOs.Users;

namespace Silver_Task.Server.Models.DTOs.Activities
{
    public class TaskActivityDto
    {
        public Guid Id { get; set; }

        /// <summary>Null if the acting user was later deleted — the event itself is kept (SetNull on delete).</summary>
        public UserSummaryDto? User { get; set; }

        public required string Action { get; set; }

        public string? FieldName { get; set; }

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
