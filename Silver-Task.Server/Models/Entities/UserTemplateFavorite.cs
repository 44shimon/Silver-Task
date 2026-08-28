namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Same polymorphic-parent shape as TemplateShare, same per-(user, template)
    /// favorite-row pattern as UserFileFavorite/UserReportFavorite — exactly one of
    /// ProjectTemplateId/TaskTemplateId is set.</summary>
    public class UserTemplateFavorite
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public Guid? ProjectTemplateId { get; set; }

        public Guid? TaskTemplateId { get; set; }

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }

        public ProjectTemplate? ProjectTemplate { get; set; }

        public TaskTemplate? TaskTemplate { get; set; }
    }
}
