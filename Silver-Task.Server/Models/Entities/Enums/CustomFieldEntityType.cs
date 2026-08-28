namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>Phase 41 — which kind of object a custom field's VALUES attach to. Distinct from
    /// CustomField.ProjectId (which scopes a field's *definition* to one project vs. every
    /// project — the existing "Organization" concept in a single-tenant app). A field with
    /// EntityType.Project describes the project itself (e.g. "Property Address"); a field with
    /// EntityType.Task describes each task within a project (e.g. "Permit Number") — same split
    /// as the spec's own Field Scope section. Every field created before this phase is
    /// EntityType.Task (see the AddCustomFieldEntityType migration's default).</summary>
    public enum CustomFieldEntityType
    {
        Task,
        Project
    }
}
