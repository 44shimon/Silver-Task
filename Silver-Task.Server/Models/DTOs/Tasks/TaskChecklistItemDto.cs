using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Tasks
{
    public static class TaskChecklistItemMappingExtensions
    {
        public static TaskChecklistItemDto ToDto(this TaskChecklistItem item) =>
            new() { Id = item.Id, Text = item.Text, IsChecked = item.IsChecked, SortOrder = item.SortOrder };
    }

    public class TaskChecklistItemDto
    {
        public Guid Id { get; set; }

        public required string Text { get; set; }

        public bool IsChecked { get; set; }

        public double SortOrder { get; set; }
    }

    public class AddChecklistItemRequest
    {
        public required string Text { get; set; }
    }

    public class SetChecklistItemCheckedRequest
    {
        public bool IsChecked { get; set; }
    }
}
