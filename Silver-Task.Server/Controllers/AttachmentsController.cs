using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Attachments;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/attachments")]
    public class AttachmentsController(IAttachmentService attachmentService) : ControllerBase
    {
        private readonly IAttachmentService _attachmentService = attachmentService;

        /// <summary>File-info panel — filename/type/size/uploader/date/location/last-modified.</summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AttachmentDto>> GetById(Guid id)
        {
            var attachment = await _attachmentService.GetByIdAsync(id, User.GetUserId(), User.GetRole());
            return Ok(attachment.ToDto());
        }

        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Download(Guid id)
        {
            var (attachment, content) = await _attachmentService.DownloadAsync(id, User.GetUserId(), User.GetRole());
            return File(content, attachment.MimeType, attachment.FileName);
        }

        /// <summary>Metadata-only rename — never touches the file on disk.</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AttachmentDto>> Rename(Guid id, [FromBody] RenameAttachmentRequest request)
        {
            var attachment = await _attachmentService.RenameAsync(id, request.FileName, User.GetUserId(), User.GetRole());
            return Ok(attachment.ToDto());
        }

        /// <summary>Soft delete — the file no longer appears normally but can be restored.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _attachmentService.DeleteAsync(id, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        /// <summary>Manage-tier only (project Manager/owner/Administrator) — see
        /// IAttachmentService.RestoreAsync's own doc comment.</summary>
        [HttpPost("{id:guid}/restore")]
        public async Task<ActionResult<AttachmentDto>> Restore(Guid id)
        {
            var attachment = await _attachmentService.RestoreAsync(id, User.GetUserId(), User.GetRole());
            return Ok(attachment.ToDto());
        }
    }
}
