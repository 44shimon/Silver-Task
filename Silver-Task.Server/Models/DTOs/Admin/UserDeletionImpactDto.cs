namespace Silver_Task.Server.Models.DTOs.Admin
{
    /// <summary>Shown in the delete-confirmation dialog before an Administrator commits to
    /// deleting a user, so they know what historical data will remain attached to the
    /// now-deleted account (deletion is a soft delete — nothing here is actually destroyed).</summary>
    public class UserDeletionImpactDto
    {
        public required string Name { get; set; }

        public required string Email { get; set; }

        public required string Role { get; set; }

        public int AssignedTaskCount { get; set; }

        public int ProjectCount { get; set; }

        public int CommentCount { get; set; }

        public int ActivityCount { get; set; }
    }
}
