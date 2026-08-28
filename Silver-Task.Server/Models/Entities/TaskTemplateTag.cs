namespace Silver_Task.Server.Models.Entities
{
    public class TaskTemplateTag
    {
        public Guid Id { get; set; }

        public Guid TaskTemplateId { get; set; }

        public Guid TagId { get; set; }

        public TaskTemplate? TaskTemplate { get; set; }

        public Tag? Tag { get; set; }
    }
}
