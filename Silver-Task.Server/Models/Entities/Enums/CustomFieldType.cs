namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>
    /// The data type of a project-defined custom task field. Determines how
    /// TaskCustomValue.Value is interpreted/validated and which editor the UI renders.
    /// </summary>
    public enum CustomFieldType
    {
        Text,
        Number,
        Currency,
        Date,
        DateTime,
        Checkbox,
        Dropdown,
        MultiSelect,
        User,
        LongText,

        /// <summary>A label + URL pair, e.g. a permit portal link or a site's homepage. Rendered as a clickable button.</summary>
        Link
    }
}
