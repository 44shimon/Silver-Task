namespace Silver_Task.Server.Models.Entities
{
    public class Project
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public Guid OwnerId { get; set; }

        public bool IsArchived { get; set; }

        public DateTime? ArchivedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        /// <summary>Phase 40 — which template (and, since templates have no separate version
        /// table, which EDIT of it via UpdatedAt) this project was generated from, if any. Purely
        /// informational/audit — SetNull on the template's deletion, since deleting a template
        /// must never affect a project already created from it (see ProjectTemplate's own doc
        /// comment). Null for every project not created from a template.</summary>
        public Guid? SourceProjectTemplateId { get; set; }

        /// <summary>The source template's own UpdatedAt at the moment this project was created —
        /// the de-facto "version" stamp (spec's own "at minimum, prevent the project from
        /// unexpectedly changing" bar is already met structurally by copying data at instantiation
        /// time; this field only answers "which edit of the template was this," it never
        /// re-applies later template changes).</summary>
        public DateTime? SourceTemplateSnapshotAt { get; set; }

        public User? Owner { get; set; }

        public ProjectTemplate? SourceProjectTemplate { get; set; }

        public ICollection<ProjectMember> Members { get; set; } = [];

        public ICollection<TaskItem> Tasks { get; set; } = [];

        public ICollection<CustomField> CustomFields { get; set; } = [];

        public ICollection<ProjectCustomValue> CustomValues { get; set; } = [];

        public ICollection<Attachment> Attachments { get; set; } = [];

        public ICollection<Folder> Folders { get; set; } = [];

        public ICollection<Automation> Automations { get; set; } = [];

        public ICollection<SavedReport> SavedReports { get; set; } = [];
    }
}
