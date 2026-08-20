namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    public class CustomFieldOptionDto
    {
        public Guid Id { get; set; }

        public required string Value { get; set; }

        public int SortOrder { get; set; }
    }
}
