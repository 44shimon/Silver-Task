namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    /// <summary>How many tasks currently hold a value for a field — fetched before a delete is
    /// attempted so the UI can warn ("used by N tasks") before the user confirms.</summary>
    public class CustomFieldUsageDto
    {
        public int TaskCount { get; set; }
    }
}
