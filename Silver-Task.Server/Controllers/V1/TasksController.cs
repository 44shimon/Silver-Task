using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Models.Common;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Models.DTOs.V1;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers.V1
{
    /// <summary>Phase 61 — the second of two reference resources for the public v1 API foundation
    /// (see Controllers/V1/ProjectsController's doc comment for the shared reasoning). Delegates
    /// every operation to the existing, unmodified ITaskService.
    ///
    /// Unlike the internal API's nested GET /api/projects/{id}/tasks, this is a flat
    /// /api/v1/tasks collection with projectId as a required query parameter — a client can then
    /// look up a single task by id (GET /api/v1/tasks/{id}) without first needing to know its
    /// project, which is the more RESTful shape for a resource future integrations address
    /// directly. ITaskService.GetAllForProjectAsync (the same method the internal
    /// ProjectsController.GetTasks already calls) returns the full authorized, unpaginated list —
    /// paging/filtering/sorting/search are applied here, over that already-authorized result, the
    /// same "reuse the service, add server-side query support only at the v1 boundary" pattern
    /// ProjectsController uses (see its own doc comment for why this doesn't touch TaskService).
    ///
    /// Phase 62 — [Authorize(Policy = "ApiKeyOrCookie")], same reasoning as
    /// Controllers/V1/ProjectsController's own doc comment.</summary>
    [ApiController]
    [Route("api/v1/tasks")]
    [Authorize(Policy = "ApiKeyOrCookie")]
    public class TasksController(ITaskService taskService) : ControllerBase
    {
        private readonly ITaskService _taskService = taskService;

        private static readonly IReadOnlyDictionary<string, Func<TaskItem, IComparable?>> SortSelectors =
            new Dictionary<string, Func<TaskItem, IComparable?>>
            {
                ["title"] = t => t.Title,
                ["duedate"] = t => t.DueDate,
                ["priority"] = t => t.Priority,
                ["status"] = t => t.Status,
                ["createdat"] = t => t.CreatedAt,
                ["updatedat"] = t => t.UpdatedAt,
            };

        [HttpGet]
        public async Task<ActionResult<PagedResult<TaskV1Dto>>> GetAll(
            [FromQuery] Guid? projectId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = ApiV1QueryOptions.DefaultPageSize,
            [FromQuery] TaskItemStatus? status = null,
            [FromQuery] TaskPriority? priority = null,
            [FromQuery] Guid? assignedToUserId = null,
            [FromQuery] string? q = null,
            [FromQuery] string? sort = null)
        {
            if (projectId is null)
            {
                throw new ValidationException("projectId is required.");
            }

            var (clampedPage, clampedPageSize) = ApiV1QueryOptions.ParsePaging(page, pageSize);
            var tasks = await _taskService.GetAllForProjectAsync(projectId.Value, User.GetUserId(), User.GetRole());

            IEnumerable<TaskItem> filtered = tasks;
            if (status is not null)
            {
                filtered = filtered.Where(t => t.Status == status);
            }
            if (priority is not null)
            {
                filtered = filtered.Where(t => t.Priority == priority);
            }
            if (assignedToUserId is not null)
            {
                filtered = filtered.Where(t => t.AssignedToUserId == assignedToUserId);
            }
            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(t => t.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (t.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var sorted = ApiV1QueryOptions.ApplySort(filtered, sort, SortSelectors) ?? filtered.OrderBy(t => t.SortOrder);
            var materialized = sorted.ToList();

            var pageItems = materialized
                .Skip((clampedPage - 1) * clampedPageSize)
                .Take(clampedPageSize)
                .Select(t => t.ToV1Dto())
                .ToList();

            return Ok(new PagedResult<TaskV1Dto>
            {
                Items = pageItems,
                Page = clampedPage,
                PageSize = clampedPageSize,
                TotalCount = materialized.Count
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TaskV1Dto>> GetById(Guid id)
        {
            var task = await _taskService.GetByIdAsync(id, User.GetUserId(), User.GetRole());
            return Ok(task.ToV1Dto());
        }

        [HttpPost]
        public async Task<ActionResult<TaskV1Dto>> Create([FromBody] CreateTaskV1Request request)
        {
            var task = await _taskService.CreateAsync(request.ProjectId,
                new CreateTaskRequest
                {
                    Title = request.Title,
                    Description = request.Description,
                    Status = request.Status,
                    Priority = request.Priority,
                    AssignedToUserId = request.AssignedToUserId,
                    StartDate = request.StartDate,
                    DueDate = request.DueDate
                },
                User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(GetById), new { id = task.Id }, task.ToV1Dto());
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<TaskV1Dto>> Update(Guid id, [FromBody] UpdateTaskV1Request request)
        {
            // SortOrder isn't part of the v1 contract (an internal drag-reorder concern, not
            // something an external API client sets) — preserve whatever it already is rather
            // than the internal UpdateTaskRequest's [Required] double defaulting to 0 and
            // silently reordering the task to the front of its list.
            var callerId = User.GetUserId();
            var callerRole = User.GetRole();
            var existing = await _taskService.GetByIdAsync(id, callerId, callerRole);

            var task = await _taskService.UpdateAsync(id,
                new UpdateTaskRequest
                {
                    Title = request.Title,
                    Description = request.Description,
                    Status = request.Status,
                    Priority = request.Priority,
                    AssignedToUserId = request.AssignedToUserId,
                    StartDate = request.StartDate,
                    DueDate = request.DueDate,
                    SortOrder = existing.SortOrder
                },
                callerId, callerRole);
            return Ok(task.ToV1Dto());
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _taskService.DeleteAsync(id, deleteSubtasks: false, User.GetUserId(), User.GetRole());
            return NoContent();
        }
    }
}
