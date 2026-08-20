using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/attachments")]
    public class AttachmentsController(IAttachmentService attachmentService) : ControllerBase
    {
        private readonly IAttachmentService _attachmentService = attachmentService;

        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Download(Guid id)
        {
            var (attachment, content) = await _attachmentService.DownloadAsync(id, User.GetUserId(), User.GetRole());
            return File(content, attachment.MimeType, attachment.FileName);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _attachmentService.DeleteAsync(id, User.GetUserId(), User.GetRole());
            return NoContent();
        }
    }
}
