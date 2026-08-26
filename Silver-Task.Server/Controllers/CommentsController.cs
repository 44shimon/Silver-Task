using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Models.DTOs.Attachments;
using Silver_Task.Server.Models.DTOs.Comments;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/comments")]
    public class CommentsController(ICommentService commentService, IAttachmentService attachmentService) : ControllerBase
    {
        private readonly ICommentService _commentService = commentService;
        private readonly IAttachmentService _attachmentService = attachmentService;

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

        [HttpGet("{id:guid}/attachments")]
        public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> GetAttachments(Guid id)
        {
            var attachments = await _attachmentService.GetAllForCommentAsync(id, User.GetUserId(), User.GetRole());
            return Ok(attachments.Select(a => a.ToDto()));
        }

        [HttpPost("{id:guid}/attachments")]
        [RequestSizeLimit(AttachmentUploadLimits.MaxRequestBodyBytes)]
        public async Task<ActionResult<AttachmentDto>> UploadAttachment(Guid id, IFormFile? file)
        {
            if (file is null)
            {
                throw new ValidationException("No file was provided.");
            }

            var attachment = await _attachmentService.UploadForCommentAsync(id, file, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(AttachmentsController.GetById), "Attachments", new { id = attachment.Id }, attachment.ToDto());
        }
    }
}
