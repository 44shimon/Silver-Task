using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Tasks;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/tasks")]
    public class TasksController(ITaskService taskService) : ControllerBase
    {
        private readonly ITaskService _taskService = taskService;

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
    }
}
