namespace Silver_Task.Server.Models.Entities
{
    public class TaskTemplateChecklistItem
    {
        public Guid Id { get; set; }

        public Guid TaskTemplateId { get; set; }

        public required string Text { get; set; }

        public double SortOrder { get; set; }

        public TaskTemplate? TaskTemplate { get; set; }
    }
}
