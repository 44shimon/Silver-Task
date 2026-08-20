using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Activities;
using Silver_Task.Server.Models.DTOs.Comments;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/tasks")]
    public class TasksController(ITaskService taskService, ICommentService commentService) : ControllerBase
    {
        private readonly ITaskService _taskService = taskService;
        private readonly ICommentService _commentService = commentService;

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
    }
}
