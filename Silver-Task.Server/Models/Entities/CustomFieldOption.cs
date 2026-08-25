namespace Silver_Task.Server.Models.Entities
{
    /// <summary>A selectable option for a Dropdown or MultiSelect custom field.</summary>
    public class CustomFieldOption
    {
        public Guid Id { get; set; }

        public Guid CustomFieldId { get; set; }

        public required string Value { get; set; }

        public int SortOrder { get; set; }

        /// <summary>Disabling an option (rather than deleting it) keeps it selectable nowhere new
        /// while leaving existing task values that reference it intact and readable.</summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public CustomField? CustomField { get; set; }
    }
}
