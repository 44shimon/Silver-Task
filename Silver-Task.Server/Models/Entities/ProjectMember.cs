namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Join entity granting a user access to a project.</summary>
    public class ProjectMember
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public Guid UserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public Project? Project { get; set; }

        public User? User { get; set; }
    }
}
