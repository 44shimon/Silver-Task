using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Models.DTOs.Activities;
using Silver_Task.Server.Models.DTOs.Attachments;
using Silver_Task.Server.Models.DTOs.Comments;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/tasks")]
    public class TasksController(ITaskService taskService, ICommentService commentService, IAttachmentService attachmentService) : ControllerBase
    {
        private readonly ITaskService _taskService = taskService;
        private readonly ICommentService _commentService = commentService;
        private readonly IAttachmentService _attachmentService = attachmentService;

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

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _taskService.DeleteAsync(id, User.GetUserId(), User.GetRole());
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
        public async Task<ActionResult<IReadOnlyList<TaskAttachmentDto>>> GetAttachments(Guid id)
        {
            var attachments = await _attachmentService.GetAllForTaskAsync(id, User.GetUserId(), User.GetRole());
            return Ok(attachments.Select(a => a.ToDto()));
        }

        // Comfortably above AttachmentService's 25 MB app-level cap, so an oversized upload gets
        // AttachmentService's clean JSON error instead of a raw framework-level rejection.
        [HttpPost("{id:guid}/attachments")]
        [RequestSizeLimit(30 * 1024 * 1024)]
        public async Task<ActionResult<TaskAttachmentDto>> UploadAttachment(Guid id, IFormFile? file)
        {
            if (file is null)
            {
                throw new ValidationException("No file was provided.");
            }

            var attachment = await _attachmentService.UploadAsync(id, file, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(GetAttachments), new { id }, attachment.ToDto());
        }
    }
}
