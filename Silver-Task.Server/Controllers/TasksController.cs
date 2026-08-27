using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Models.DTOs.Activities;
using Silver_Task.Server.Models.DTOs.Attachments;
using Silver_Task.Server.Models.DTOs.Comments;
using Silver_Task.Server.Models.DTOs.Dependencies;
using Silver_Task.Server.Models.DTOs.Recurrence;
using Silver_Task.Server.Models.DTOs.Tags;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/tasks")]
    public class TasksController(
        ITaskService taskService,
        ICommentService commentService,
        IAttachmentService attachmentService,
        ITaskDependencyService dependencyService,
        IRecurringTaskService recurringTaskService) : ControllerBase
    {
        private readonly ITaskService _taskService = taskService;
        private readonly ICommentService _commentService = commentService;
        private readonly IAttachmentService _attachmentService = attachmentService;
        private readonly ITaskDependencyService _dependencyService = dependencyService;
        private readonly IRecurringTaskService _recurringTaskService = recurringTaskService;

        /// <summary>Global task search (Topbar) — case-insensitive partial match across title,
        /// description, project name, assignee name, and Text/LongText custom fields, scoped to
        /// projects the caller can access and capped server-side so the browser never has to
        /// download a large result set just to filter it.</summary>
        [HttpGet("search")]
        public async Task<ActionResult<IReadOnlyList<TaskDto>>> Search([FromQuery] string q)
        {
            var tasks = await _taskService.SearchAsync(q ?? string.Empty, User.GetUserId(), User.GetRole());
            return Ok(tasks.Select(t => t.ToDto()));
        }

        /// <summary>Backs the "My Tasks" dashboard — every task assigned to the caller across all
        /// their projects. The literal "my"/"search" segments never collide with {id:guid} below,
        /// since a route with a guid constraint can't match a non-guid literal.</summary>
        [HttpGet("my")]
        public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetMyTasks()
        {
            var tasks = await _taskService.GetAssignedToUserAsync(User.GetUserId(), User.GetRole());
            return Ok(tasks.Select(t => t.ToDto()));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TaskDto>> GetById(Guid id)
        {
            var task = await _taskService.GetByIdAsync(id, User.GetUserId(), User.GetRole());
            return Ok(task.ToDto());
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<TaskDto>> Update(Guid id, [FromBody] UpdateTaskRequest request)
        {
            var task = await _taskService.UpdateAsync(id, request, User.GetUserId(), User.GetRole());
            return Ok(task.ToDto());
        }

        /// <param name="deleteSubtasks">False (default): direct children are reparented to this
        /// task's own parent and preserved. True: the entire subtree is deleted with it. The
        /// frontend only ever sends true after the user explicitly picks "Delete Task + All
        /// Subtasks" in the confirmation dialog.</param>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, [FromQuery] bool deleteSubtasks = false)
        {
            await _taskService.DeleteAsync(id, deleteSubtasks, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpPost("{id:guid}/duplicate")]
        public async Task<ActionResult<TaskDto>> Duplicate(Guid id)
        {
            var copy = await _taskService.DuplicateAsync(id, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(GetById), new { id = copy.Id }, copy.ToDto());
        }

        [HttpPut("{id:guid}/custom-values/{customFieldId:guid}")]
        public async Task<ActionResult<TaskDto>> SetCustomValue(Guid id, Guid customFieldId, [FromBody] SetTaskCustomValueRequest request)
        {
            var task = await _taskService.SetCustomValueAsync(id, customFieldId, request.Value, User.GetUserId(), User.GetRole());
            return Ok(task.ToDto());
        }

        [HttpGet("{id:guid}/activities")]
        public async Task<ActionResult<IReadOnlyList<TaskActivityDto>>> GetActivities(Guid id)
        {
            var activities = await _taskService.GetActivitiesForTaskAsync(id, User.GetUserId(), User.GetRole());
            return Ok(activities.Select(a => a.ToDto()));
        }

        [HttpGet("{id:guid}/comments")]
        public async Task<ActionResult<IReadOnlyList<CommentDto>>> GetComments(Guid id)
        {
            var comments = await _commentService.GetAllForTaskAsync(id, User.GetUserId(), User.GetRole());
            return Ok(comments.Select(c => c.ToDto()));
        }

        [HttpPost("{id:guid}/comments")]
        public async Task<ActionResult<CommentDto>> CreateComment(Guid id, [FromBody] CreateCommentRequest request)
        {
            var comment = await _commentService.CreateAsync(id, request.Text, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(GetComments), new { id }, comment.ToDto());
        }

        [HttpGet("{id:guid}/attachments")]
        public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> GetAttachments(Guid id)
        {
            var callerId = User.GetUserId();
            var attachments = await _attachmentService.GetAllForTaskAsync(id, callerId, User.GetRole());
            var favoritedIds = await _attachmentService.GetFavoritedFileIdsAsync(callerId, attachments.Select(a => a.Id));
            return Ok(attachments.Select(a => a.ToDto(favoritedIds.Contains(a.Id))));
        }

        // AttachmentUploadLimits.MaxRequestBodyBytes is comfortably above the admin-configurable
        // Attachments.MaxSizeMb app-level cap, so an oversized upload gets AttachmentService's
        // clean JSON error instead of a raw framework-level rejection.
        [HttpPost("{id:guid}/attachments")]
        [RequestSizeLimit(AttachmentUploadLimits.MaxRequestBodyBytes)]
        public async Task<ActionResult<AttachmentDto>> UploadAttachment(Guid id, IFormFile? file)
        {
            if (file is null)
            {
                throw new ValidationException("No file was provided.");
            }

            var attachment = await _attachmentService.UploadForTaskAsync(id, file, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(AttachmentsController.GetById), "Attachments", new { id = attachment.Id }, attachment.ToDto());
        }

        /// <summary>The "Depends On" list — prerequisites of this task.</summary>
        [HttpGet("{id:guid}/dependencies")]
        public async Task<ActionResult<IReadOnlyList<TaskDependencyDto>>> GetDependencies(Guid id)
        {
            var dependencies = await _dependencyService.GetDependenciesAsync(id, User.GetUserId(), User.GetRole());
            return Ok(dependencies.Select(d => d.ToDependsOnDto()));
        }

        /// <summary>The "Blocking" list — tasks that depend on this one.</summary>
        [HttpGet("{id:guid}/dependents")]
        public async Task<ActionResult<IReadOnlyList<TaskDependencyDto>>> GetDependents(Guid id)
        {
            var dependents = await _dependencyService.GetDependentsAsync(id, User.GetUserId(), User.GetRole());
            return Ok(dependents.Select(d => d.ToDependentDto()));
        }

        [HttpPost("{id:guid}/dependencies")]
        public async Task<ActionResult<TaskDependencyDto>> CreateDependency(Guid id, [FromBody] CreateTaskDependencyRequest request)
        {
            var dependency = await _dependencyService.CreateAsync(
                id, request.DependsOnTaskId, request.ResolvedDependencyType, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(GetDependencies), new { id }, dependency.ToDependsOnDto());
        }

        [HttpDelete("{id:guid}/dependencies/{dependencyId:guid}")]
        public async Task<IActionResult> DeleteDependency(Guid id, Guid dependencyId)
        {
            await _dependencyService.DeleteAsync(id, dependencyId, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        /// <summary>Direct children only, not the full recursive subtree.</summary>
        [HttpGet("{id:guid}/subtasks")]
        public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetSubtasks(Guid id)
        {
            var subtasks = await _taskService.GetSubtasksAsync(id, User.GetUserId(), User.GetRole());
            return Ok(subtasks.Select(t => t.ToDto()));
        }

        /// <summary>ProjectId and ParentTaskId are resolved from the parent task server-side —
        /// never accepted from the request body.</summary>
        [HttpPost("{id:guid}/subtasks")]
        public async Task<ActionResult<TaskDto>> CreateSubtask(Guid id, [FromBody] CreateTaskRequest request)
        {
            var subtask = await _taskService.CreateSubtaskAsync(id, request, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(GetById), new { id = subtask.Id }, subtask.ToDto());
        }

        /// <summary>The "Move Task" action — null ParentTaskId moves the task to top level.</summary>
        [HttpPut("{id:guid}/parent")]
        public async Task<ActionResult<TaskDto>> SetParent(Guid id, [FromBody] SetTaskParentRequest request)
        {
            var task = await _taskService.SetParentAsync(id, request.ParentTaskId, User.GetUserId(), User.GetRole());
            return Ok(task.ToDto());
        }

        [HttpPut("{id:guid}/sort-order")]
        public async Task<ActionResult<TaskDto>> SetSortOrder(Guid id, [FromBody] SetTaskSortOrderRequest request)
        {
            var task = await _taskService.SetSortOrderAsync(id, request.SortOrder, User.GetUserId(), User.GetRole());
            return Ok(task.ToDto());
        }

        /// <summary>Null (200 with an empty body's worth of "not recurring") rather than 404 — "is
        /// this task recurring" is a routine yes/no question the Task Detail panel asks for every
        /// task it opens, not an error case.</summary>
        [HttpGet("{id:guid}/recurrence")]
        public async Task<ActionResult<RecurrenceRuleDto?>> GetRecurrence(Guid id)
        {
            var rule = await _recurringTaskService.GetForTaskAsync(id, User.GetUserId(), User.GetRole());
            return Ok(rule?.ToDto());
        }

        /// <summary>Attaches a recurrence rule to an existing task, which becomes the series'
        /// template/first occurrence.</summary>
        [HttpPost("{id:guid}/recurrence")]
        public async Task<ActionResult<RecurrenceRuleDto>> CreateRecurrence(Guid id, [FromBody] CreateRecurrenceRequest request)
        {
            var rule = await _recurringTaskService.CreateAsync(id, request, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(GetRecurrence), new { id }, rule.ToDto());
        }

        /// <summary>id can be any occurrence in the series, not just the template.</summary>
        [HttpPut("{id:guid}/recurrence")]
        public async Task<ActionResult<RecurrenceRuleDto>> UpdateRecurrence(Guid id, [FromBody] UpdateRecurrenceRequest request)
        {
            var rule = await _recurringTaskService.UpdateAsync(id, request, User.GetUserId(), User.GetRole());
            return Ok(rule.ToDto());
        }

        /// <summary>Hard-deletes the recurrence rule itself. Every already-generated task is kept —
        /// it simply stops being linked to a series (see TaskItemConfiguration's SetNull FK).</summary>
        [HttpDelete("{id:guid}/recurrence")]
        public async Task<IActionResult> DeleteRecurrence(Guid id)
        {
            await _recurringTaskService.DeleteAsync(id, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        /// <summary>Existing generated tasks are untouched; only future generation halts.</summary>
        [HttpPost("{id:guid}/recurrence/stop")]
        public async Task<ActionResult<RecurrenceRuleDto>> StopRecurrence(Guid id)
        {
            var rule = await _recurringTaskService.StopAsync(id, User.GetUserId(), User.GetRole());
            return Ok(rule.ToDto());
        }

        [HttpPost("{id:guid}/recurrence/resume")]
        public async Task<ActionResult<RecurrenceRuleDto>> ResumeRecurrence(Guid id)
        {
            var rule = await _recurringTaskService.ResumeAsync(id, User.GetUserId(), User.GetRole());
            return Ok(rule.ToDto());
        }

        /// <summary>Every task ever generated by this series (including the template), oldest
        /// first — backs "View series".</summary>
        [HttpGet("{id:guid}/recurrence/series")]
        public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetRecurrenceSeries(Guid id)
        {
            var series = await _recurringTaskService.GetSeriesAsync(id, User.GetUserId(), User.GetRole());
            return Ok(series.Select(t => t.ToDto()));
        }

        /// <summary>"Labels" (Phase 35) — reuses the same global Tag vocabulary Phase 34
        /// introduced for files (see TaskTag's own doc comment).</summary>
        [HttpGet("{id:guid}/labels")]
        public async Task<ActionResult<IReadOnlyList<TagDto>>> GetLabels(Guid id)
        {
            var labels = await _taskService.GetLabelsAsync(id, User.GetUserId(), User.GetRole());
            return Ok(labels.Select(t => t.ToDto()));
        }

        [HttpPost("{id:guid}/labels")]
        public async Task<ActionResult<TagDto>> AddLabel(Guid id, [FromBody] AddTagRequest request)
        {
            var tag = await _taskService.AddLabelAsync(id, request.Name, User.GetUserId(), User.GetRole());
            return Ok(tag.ToDto());
        }

        [HttpDelete("{id:guid}/labels/{tagId:guid}")]
        public async Task<IActionResult> RemoveLabel(Guid id, Guid tagId)
        {
            await _taskService.RemoveLabelAsync(id, tagId, User.GetUserId(), User.GetRole());
            return NoContent();
        }
    }
}
