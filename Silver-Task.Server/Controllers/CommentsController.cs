using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Comments;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/comments")]
    public class CommentsController(ICommentService commentService) : ControllerBase
    {
        private readonly ICommentService _commentService = commentService;

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<CommentDto>> Update(Guid id, [FromBody] UpdateCommentRequest request)
        {
            var comment = await _commentService.UpdateAsync(id, request.Text, User.GetUserId());
            return Ok(comment.ToDto());
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _commentService.DeleteAsync(id, User.GetUserId());
            return NoContent();
        }
    }
}
