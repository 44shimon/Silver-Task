using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.CustomFields;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Cross-project custom field management. Listing/creating a field with no
    /// project (applies everywhere) needs an entry point outside the project-scoped
    /// POST /api/projects/{id}/custom-fields — editing, deleting, and option management for
    /// any field (project-scoped or global) reuse the existing CustomFieldsController endpoints
    /// unchanged, since CustomFieldService already authorizes those per-field regardless of
    /// which controller called in.</summary>
    [ApiController]
    [Route("api/admin/custom-fields")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public class AdminCustomFieldsController(ICustomFieldService customFieldService) : ControllerBase
    {
        private readonly ICustomFieldService _customFieldService = customFieldService;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<CustomFieldDto>>> GetAll(
            [FromQuery] Guid? projectId,
            [FromQuery] CustomFieldType? fieldType,
            [FromQuery] CustomFieldEntityType? entityType,
            [FromQuery] bool? isActive)
        {
            var fields = await _customFieldService.GetAllForAdminAsync(projectId, fieldType, entityType, isActive);
            return Ok(fields.Select(f => f.ToDto()));
        }

        [HttpPost("reorder")]
        public async Task<IActionResult> Reorder([FromBody] List<Guid> orderedFieldIds)
        {
            await _customFieldService.ReorderAsync(orderedFieldIds, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<CustomFieldDto>> Create([FromBody] AdminCreateCustomFieldRequest request)
        {
            var field = await _customFieldService.CreateAsync(request.ProjectId, request, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(CustomFieldsController.GetById), "CustomFields", new { id = field.Id }, field.ToDto());
        }
    }
}
