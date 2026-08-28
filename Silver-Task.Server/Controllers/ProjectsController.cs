using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Models.Common;
using Silver_Task.Server.Models.DTOs.Attachments;
using Silver_Task.Server.Models.DTOs.Automations;
using Silver_Task.Server.Models.DTOs.CustomFields;
using Silver_Task.Server.Models.DTOs.Dependencies;
using Silver_Task.Server.Models.DTOs.Folders;
using Silver_Task.Server.Models.DTOs.Projects;
using Silver_Task.Server.Models.DTOs.Recurrence;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/projects")]
    public class ProjectsController(
        IProjectService projectService,
        ITaskService taskService,
        ICustomFieldService customFieldService,
        ITaskDependencyService dependencyService,
        IRecurringTaskService recurringTaskService,
        IPermissionService permissionService,
        IAttachmentService attachmentService,
        IFolderService folderService,
        IAutomationService automationService) : ControllerBase
    {
        private readonly IProjectService _projectService = projectService;
        private readonly ITaskService _taskService = taskService;
        private readonly ICustomFieldService _customFieldService = customFieldService;
        private readonly ITaskDependencyService _dependencyService = dependencyService;
        private readonly IRecurringTaskService _recurringTaskService = recurringTaskService;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly IAttachmentService _attachmentService = attachmentService;
        private readonly IFolderService _folderService = folderService;
        private readonly IAutomationService _automationService = automationService;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetAll([FromQuery] bool includeArchived = false)
        {
            var projects = await _projectService.GetAllForUserAsync(User.GetUserId(), User.GetRole(), includeArchived);
            var taskCounts = await _taskService.GetTaskCountsByProjectAsync(projects.Select(p => p.Id));
            return Ok(projects.Select(p => p.ToDto(taskCounts.GetValueOrDefault(p.Id))));
        }

        [HttpPost]
        public async Task<ActionResult<ProjectDto>> Create([FromBody] CreateProjectRequest request)
        {
            var project = await _projectService.CreateAsync(request, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(GetById), new { id = project.Id }, project.ToDto());
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProjectDto>> GetById(Guid id)
        {
            var project = await _projectService.GetByIdAsync(id, User.GetUserId(), User.GetRole());
            var myPermissions = await _permissionService.GetProjectPermissionsAsync(project.Id, project.OwnerId, User.GetUserId(), User.GetRole());
            return Ok(project.ToDto(myPermissions: [.. myPermissions]));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ProjectDto>> Update(Guid id, [FromBody] UpdateProjectRequest request)
        {
            var project = await _projectService.UpdateAsync(id, request, User.GetUserId(), User.GetRole());
            return Ok(project.ToDto());
        }

        /// <summary>Archives the project (soft delete) rather than removing its row.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Archive(Guid id)
        {
            await _projectService.ArchiveAsync(id, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpPost("{id:guid}/restore")]
        public async Task<ActionResult<ProjectDto>> Restore(Guid id)
        {
            var project = await _projectService.RestoreAsync(id, User.GetUserId(), User.GetRole());
            return Ok(project.ToDto());
        }

        [HttpGet("{id:guid}/members")]
        public async Task<ActionResult<IReadOnlyList<ProjectMemberDto>>> GetMembers(Guid id)
        {
            var members = await _projectService.GetMembersAsync(id, User.GetUserId(), User.GetRole());
            return Ok(members.Select(m => m.ToDto()));
        }

        [HttpPost("{id:guid}/members")]
        public async Task<ActionResult<ProjectMemberDto>> AddMember(Guid id, [FromBody] AddProjectMemberRequest request)
        {
            var member = await _projectService.AddMemberAsync(id, request.Email, User.GetUserId(), User.GetRole());
            if (member is null)
            {
                // A plain 404 return, not a thrown/caught NotFoundException — typing an email
                // with no account yet is routine user input here, not an application error, so
                // this shouldn't behave like (or debug-break like) the "real" not-found cases
                // elsewhere in the app (a stale project/task id, a tampered request, etc.).
                return NotFound(new ApiErrorResponse
                {
                    Message = $"No user found with email '{request.Email}'.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            return CreatedAtAction(nameof(GetMembers), new { id }, member.ToDto());
        }

        [HttpDelete("{id:guid}/members/{userId:guid}")]
        public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
        {
            await _projectService.RemoveMemberAsync(id, userId, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        /// <summary>Changes a member's project-scoped role (Manager/Member/Viewer) — see
        /// ProjectMember.Role / ProjectRole. Manage-tier, same as adding/removing members.</summary>
        [HttpPut("{id:guid}/members/{userId:guid}/role")]
        public async Task<ActionResult<ProjectMemberDto>> SetMemberRole(Guid id, Guid userId, [FromBody] SetProjectMemberRoleRequest request)
        {
            var member = await _projectService.SetMemberRoleAsync(id, userId, request.Role, User.GetUserId(), User.GetRole());
            return Ok(member.ToDto());
        }

        [HttpGet("{id:guid}/tasks")]
        public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetTasks(Guid id)
        {
            var tasks = await _taskService.GetAllForProjectAsync(id, User.GetUserId(), User.GetRole());
            return Ok(tasks.Select(t => t.ToDto()));
        }

        [HttpPost("{id:guid}/tasks")]
        public async Task<ActionResult<TaskDto>> CreateTask(Guid id, [FromBody] CreateTaskRequest request)
        {
            var task = await _taskService.CreateAsync(id, request, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(TasksController.GetById), "Tasks", new { id = task.Id }, task.ToDto());
        }

        [HttpGet("{id:guid}/custom-fields")]
        public async Task<ActionResult<IReadOnlyList<CustomFieldDto>>> GetCustomFields(Guid id, [FromQuery] CustomFieldEntityType entityType = CustomFieldEntityType.Task)
        {
            var fields = await _customFieldService.GetAllForProjectAsync(id, entityType, User.GetUserId(), User.GetRole());
            return Ok(fields.Select(f => f.ToDto()));
        }

        [HttpPost("{id:guid}/custom-fields")]
        public async Task<ActionResult<CustomFieldDto>> CreateCustomField(Guid id, [FromBody] CreateCustomFieldRequest request)
        {
            var field = await _customFieldService.CreateAsync(id, request, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(CustomFieldsController.GetById), "CustomFields", new { id = field.Id }, field.ToDto());
        }

        [HttpPut("{id:guid}/custom-values/{customFieldId:guid}")]
        public async Task<ActionResult<ProjectDto>> SetCustomValue(Guid id, Guid customFieldId, [FromBody] SetTaskCustomValueRequest request)
        {
            var project = await _projectService.SetCustomValueAsync(id, customFieldId, request.Value, User.GetUserId(), User.GetRole());
            return Ok(project.ToDto());
        }

        /// <summary>Field-level authorization (manage tier) is enforced inside
        /// ICustomFieldService.ReorderAsync itself — this route has no [Authorize(Roles=...)]
        /// gate of its own so a project Manager can reorder their own project's fields, not just
        /// an Administrator (see AdminCustomFieldsController's own reorder route for the
        /// Administrator-facing equivalent on global fields).</summary>
        [HttpPost("{id:guid}/custom-fields/reorder")]
        public async Task<IActionResult> ReorderCustomFields(Guid id, [FromBody] List<Guid> orderedFieldIds)
        {
            await _customFieldService.ReorderAsync(orderedFieldIds, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        /// <summary>Every dependency edge in the project, for Gantt/Timeline connector-line
        /// rendering — one request instead of one per visible bar (the Task data for each row is
        /// already loaded via GetTasks; this only adds which pairs are connected).</summary>
        [HttpGet("{id:guid}/dependencies")]
        public async Task<ActionResult<IReadOnlyList<TaskDependencyEdgeDto>>> GetDependencyEdges(Guid id)
        {
            var edges = await _dependencyService.GetProjectEdgesAsync(id, User.GetUserId(), User.GetRole());
            return Ok(edges.Select(e => new TaskDependencyEdgeDto { TaskId = e.TaskId, DependsOnTaskId = e.DependsOnTaskId }));
        }

        /// <summary>Every recurring rule defined in the project (active and stopped) — backs the
        /// Recurring Tasks management list.</summary>
        [HttpGet("{id:guid}/recurring-tasks")]
        public async Task<ActionResult<IReadOnlyList<RecurrenceRuleDto>>> GetRecurringTasks(Guid id)
        {
            var rules = await _recurringTaskService.GetForProjectAsync(id, User.GetUserId(), User.GetRole());
            return Ok(rules.Select(r => r.ToDto()));
        }

        /// <summary>Project → Files. onlyDeleted=true (Manage-tier only) backs the Restore UI —
        /// see IAttachmentService.GetAllForProjectAsync's own doc comment. folderId/includeSubfolders
        /// drive folder navigation (Phase 34) — omitting folderId means "root level"; pass
        /// includeSubfolders=true for a whole-project search scope.</summary>
        [HttpGet("{id:guid}/files")]
        public async Task<ActionResult<AttachmentListDto>> GetFiles(
            Guid id, [FromQuery] bool onlyDeleted = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null, [FromQuery] string? type = null, [FromQuery] Guid? uploadedByUserId = null,
            [FromQuery] DateTime? dateFrom = null, [FromQuery] DateTime? dateTo = null,
            [FromQuery] string? sortField = null, [FromQuery] bool sortDescending = true,
            [FromQuery] Guid? folderId = null, [FromQuery] bool includeSubfolders = false,
            [FromQuery] Guid? categoryId = null, [FromQuery] Guid? tagId = null, [FromQuery] bool favoritesOnly = false)
        {
            var callerId = User.GetUserId();
            var (items, totalCount) = await _attachmentService.GetAllForProjectAsync(
                id, callerId, User.GetRole(), onlyDeleted, page, pageSize,
                search, type, uploadedByUserId, dateFrom, dateTo, sortField, sortDescending,
                folderId, includeSubfolders, categoryId, tagId, favoritesOnly);

            var favoritedIds = await _attachmentService.GetFavoritedFileIdsAsync(callerId, items.Select(a => a.Id));

            return Ok(new AttachmentListDto
            {
                Items = [.. items.Select(a => a.ToDto(favoritedIds.Contains(a.Id)))],
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        [HttpPost("{id:guid}/files")]
        [RequestSizeLimit(AttachmentUploadLimits.MaxRequestBodyBytes)]
        public async Task<ActionResult<AttachmentDto>> UploadFile(Guid id, IFormFile? file, [FromForm] Guid? folderId = null)
        {
            if (file is null)
            {
                throw new ValidationException("No file was provided.");
            }

            var attachment = await _attachmentService.UploadForProjectAsync(id, file, User.GetUserId(), User.GetRole(), folderId);
            return CreatedAtAction(nameof(AttachmentsController.GetById), "Attachments", new { id = attachment.Id }, attachment.ToDto());
        }

        /// <summary>Flat list (parent-linked, not a nested tree) — the frontend builds the tree/
        /// breadcrumbs client-side from ParentFolderId, same pattern already established for
        /// subtask hierarchy (see TaskBreadcrumb/getTaskAncestors).</summary>
        [HttpGet("{id:guid}/folders")]
        public async Task<ActionResult<IReadOnlyList<FolderDto>>> GetFolders(Guid id, [FromQuery] bool includeDeleted = false)
        {
            var folders = await _folderService.GetAllForProjectAsync(id, User.GetUserId(), User.GetRole(), includeDeleted);
            return Ok(folders.Select(f => f.ToDto()));
        }

        [HttpPost("{id:guid}/folders")]
        public async Task<ActionResult<FolderDto>> CreateFolder(Guid id, [FromBody] CreateFolderRequest request)
        {
            var folder = await _folderService.CreateAsync(id, request.Name, request.ParentFolderId, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(FoldersController.GetById), "Folders", new { id = folder.Id }, folder.ToDto());
        }

        /// <summary>Project → Automations. Viewable by any project member (including a Viewer,
        /// per Automations.View — see PermissionService's ProjectMatrix); creating one still
        /// requires Manage-tier (enforced inside AutomationService, not here).</summary>
        [HttpGet("{id:guid}/automations")]
        public async Task<ActionResult<IReadOnlyList<AutomationDto>>> GetAutomations(
            Guid id, [FromQuery] string? search = null, [FromQuery] AutomationTriggerType? triggerType = null,
            [FromQuery] bool? isActive = null, [FromQuery] Guid? createdByUserId = null)
        {
            var automations = await _automationService.GetAllForProjectAsync(
                id, User.GetUserId(), User.GetRole(), search, triggerType, isActive, createdByUserId);
            return Ok(automations.Select(a => a.ToDto()));
        }

        [HttpPost("{id:guid}/automations")]
        public async Task<ActionResult<AutomationDto>> CreateAutomation(Guid id, [FromBody] SaveAutomationRequest request)
        {
            request.ProjectId = id;
            var automation = await _automationService.CreateAsync(request, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(AutomationsController.GetById), "Automations", new { id = automation.Id }, automation.ToDto());
        }
    }
}
