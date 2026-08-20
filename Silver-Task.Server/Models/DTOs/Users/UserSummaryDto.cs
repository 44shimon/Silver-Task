namespace Silver_Task.Server.Models.DTOs.Users
{
    /// <summary>Minimal user shape nested inside other resources (project owner, member, assignee, ...).</summary>
    public class UserSummaryDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public required string Email { get; set; }
    }
}
