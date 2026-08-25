namespace Silver_Task.Server.Models.DTOs.Users
{
    /// <summary>Minimal user shape nested inside other resources (project owner, member, assignee, ...).</summary>
    public class UserSummaryDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public required string Email { get; set; }

        /// <summary>So consumers like the assignee dropdown can exclude deactivated/deleted
        /// members from new selections while still showing them wherever they're already
        /// referenced (existing assignments, comment authors, activity history, ...).</summary>
        public bool IsActive { get; set; }
    }
}
