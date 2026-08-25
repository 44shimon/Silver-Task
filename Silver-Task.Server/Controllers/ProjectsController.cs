using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.Common;
using Silver_Task.Server.Models.DTOs.CustomFields;
using Silver_Task.Server.Models.DTOs.Projects;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/projects")]
    public class ProjectsController(IProjectService projectService, ITaskService taskService, ICustomFieldService customFieldService) : ControllerBase
    {
        private readonly IProjectService _projectService = projectService;
        private readonly ITaskService _taskService = taskService;
        private readonly ICustomFieldService _customFieldService = customFieldService;

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
            return Ok(project.ToDto());
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
        public async Task<ActionResult<IReadOnlyList<CustomFieldDto>>> GetCustomFields(Guid id)
        {
            var fields = await _customFieldService.GetAllForProjectAsync(id, User.GetUserId(), User.GetRole());
            return Ok(fields.Select(f => f.ToDto()));
        }

        [HttpPost("{id:guid}/custom-fields")]
        public async Task<ActionResult<CustomFieldDto>> CreateCustomField(Guid id, [FromBody] CreateCustomFieldRequest request)
        {
            var field = await _customFieldService.CreateAsync(id, request, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(CustomFieldsController.GetById), "CustomFields", new { id = field.Id }, field.ToDto());
        }
    }
}
