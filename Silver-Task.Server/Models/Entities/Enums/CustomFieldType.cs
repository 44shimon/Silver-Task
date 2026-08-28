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
        Link,

        /// <summary>Phase 41 — a bare URL with no label (unlike Link). Reuses Link's own scheme
        /// normalization/validation (http/https only, rejects javascript:/data: etc.).</summary>
        Url,

        /// <summary>Phase 41 — validated via .NET's own MailAddress parser, not a hand-rolled regex.</summary>
        Email,

        /// <summary>Phase 41 — a lightweight permissive format check (digits/spaces/+/-/()), since
        /// no phone-formatting system exists anywhere else in this app to reuse.</summary>
        Phone,

        /// <summary>Phase 41 — like User, but multiple project members. Stored the same way
        /// MultiSelect stores its option ids: a JSON array of user ids, never a comma-separated
        /// string.</summary>
        UserMulti,

        /// <summary>Phase 41 — references another Task by its real Id (never by title/name).</summary>
        TaskReference,

        /// <summary>Phase 41 — references another Project by its real Id (never by name).</summary>
        ProjectReference
    }
}
