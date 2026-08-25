namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    /// <summary>Only the Admin > Custom Fields page uses this — everywhere else a field is
    /// created via the project-scoped POST /api/projects/{id}/custom-fields, which always passes
    /// the project id from the route rather than trusting a body field.</summary>
    public class AdminCreateCustomFieldRequest : CreateCustomFieldRequest
    {
        /// <summary>Null = applies to every project (Administrator-only).</summary>
        public Guid? ProjectId { get; set; }
    }
}
