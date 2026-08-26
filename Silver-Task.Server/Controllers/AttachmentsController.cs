using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Attachments;
using Silver_Task.Server.Models.DTOs.Tags;
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
            var callerId = User.GetUserId();
            var attachment = await _attachmentService.GetByIdAsync(id, callerId, User.GetRole());
            var favoritedIds = await _attachmentService.GetFavoritedFileIdsAsync(callerId, [id]);
            return Ok(attachment.ToDto(favoritedIds.Contains(id)));
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

        /// <summary>Move a file to a different folder (or, with a null FolderId, back to the
        /// project's root level) — Phase 34. Metadata-only, never touches StoragePath.</summary>
        [HttpPost("{id:guid}/move")]
        public async Task<ActionResult<AttachmentDto>> Move(Guid id, [FromBody] MoveAttachmentRequest request)
        {
            var attachment = await _attachmentService.MoveAsync(id, request.FolderId, User.GetUserId(), User.GetRole());
            return Ok(attachment.ToDto());
        }

        [HttpPut("{id:guid}/description")]
        public async Task<ActionResult<AttachmentDto>> UpdateDescription(Guid id, [FromBody] UpdateDescriptionRequest request)
        {
            var attachment = await _attachmentService.UpdateDescriptionAsync(id, request.Description, User.GetUserId(), User.GetRole());
            return Ok(attachment.ToDto());
        }

        [HttpPut("{id:guid}/category")]
        public async Task<ActionResult<AttachmentDto>> SetCategory(Guid id, [FromBody] SetCategoryRequest request)
        {
            var attachment = await _attachmentService.SetCategoryAsync(id, request.CategoryId, User.GetUserId(), User.GetRole());
            return Ok(attachment.ToDto());
        }

        [HttpGet("{id:guid}/tags")]
        public async Task<ActionResult<IReadOnlyList<TagDto>>> GetTags(Guid id)
        {
            var tags = await _attachmentService.GetTagsAsync(id, User.GetUserId(), User.GetRole());
            return Ok(tags.Select(t => t.ToDto()));
        }

        /// <summary>Get-or-create by name — see ITagService.GetOrCreateAsync's own doc comment.</summary>
        [HttpPost("{id:guid}/tags")]
        public async Task<ActionResult<TagDto>> AddTag(Guid id, [FromBody] AddTagRequest request)
        {
            var tag = await _attachmentService.AddTagAsync(id, request.Name, User.GetUserId(), User.GetRole());
            return Ok(tag.ToDto());
        }

        [HttpDelete("{id:guid}/tags/{tagId:guid}")]
        public async Task<IActionResult> RemoveTag(Guid id, Guid tagId)
        {
            await _attachmentService.RemoveTagAsync(id, tagId, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpPost("{id:guid}/favorite")]
        public async Task<IActionResult> Favorite(Guid id)
        {
            await _attachmentService.ToggleFavoriteAsync(id, true, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpDelete("{id:guid}/favorite")]
        public async Task<IActionResult> Unfavorite(Guid id)
        {
            await _attachmentService.ToggleFavoriteAsync(id, false, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        /// <summary>Files -> Favorites — every file the caller has favorited that they can still
        /// currently access (re-checked live, see IAttachmentService.GetFavoritesAsync).</summary>
        [HttpGet("favorites")]
        public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> GetFavorites()
        {
            var attachments = await _attachmentService.GetFavoritesAsync(User.GetUserId(), User.GetRole());
            return Ok(attachments.Select(a => a.ToDto(isFavorite: true)));
        }

        /// <summary>Files -> Recent — files the caller has uploaded or modified most recently,
        /// limited to projects they can still access.</summary>
        [HttpGet("recent")]
        public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> GetRecent([FromQuery] int limit = 50)
        {
            var callerId = User.GetUserId();
            var attachments = await _attachmentService.GetRecentAsync(callerId, User.GetRole(), limit);
            var favoritedIds = await _attachmentService.GetFavoritedFileIdsAsync(callerId, attachments.Select(a => a.Id));
            return Ok(attachments.Select(a => a.ToDto(favoritedIds.Contains(a.Id))));
        }

        // Bulk actions (Phase 34) — each one reruns the exact same per-file service call the
        // single-file endpoint above uses, so permission/validation can never be bypassed by
        // batching; a failure on one file is collected and reported, not silently swallowed or
        // allowed to abort the rest of the selection. See BulkActionResultDto's own doc comment.

        [HttpPost("bulk/move")]
        public async Task<ActionResult<BulkActionResultDto>> BulkMove([FromBody] BulkMoveRequest request)
        {
            var callerId = User.GetUserId();
            var callerRole = User.GetRole();
            return Ok(await RunBulkAsync(request.FileIds, id => _attachmentService.MoveAsync(id, request.FolderId, callerId, callerRole)));
        }

        [HttpPost("bulk/tag")]
        public async Task<ActionResult<BulkActionResultDto>> BulkTag([FromBody] BulkTagRequest request)
        {
            var callerId = User.GetUserId();
            var callerRole = User.GetRole();
            return Ok(await RunBulkAsync(request.FileIds, id => _attachmentService.AddTagAsync(id, request.TagName, callerId, callerRole)));
        }

        [HttpPost("bulk/untag")]
        public async Task<ActionResult<BulkActionResultDto>> BulkUntag([FromBody] BulkUntagRequest request)
        {
            var callerId = User.GetUserId();
            var callerRole = User.GetRole();
            return Ok(await RunBulkAsync(request.FileIds, id => _attachmentService.RemoveTagAsync(id, request.TagId, callerId, callerRole)));
        }

        [HttpPost("bulk/delete")]
        public async Task<ActionResult<BulkActionResultDto>> BulkDelete([FromBody] BulkDeleteRequest request)
        {
            var callerId = User.GetUserId();
            var callerRole = User.GetRole();
            return Ok(await RunBulkAsync(request.FileIds, id => _attachmentService.DeleteAsync(id, callerId, callerRole)));
        }

        [HttpPost("bulk/favorite")]
        public async Task<ActionResult<BulkActionResultDto>> BulkFavorite([FromBody] BulkFavoriteRequest request)
        {
            var callerId = User.GetUserId();
            var callerRole = User.GetRole();
            return Ok(await RunBulkAsync(request.FileIds, id => _attachmentService.ToggleFavoriteAsync(id, request.Favorite, callerId, callerRole)));
        }

        private static async Task<BulkActionResultDto> RunBulkAsync(List<Guid> fileIds, Func<Guid, Task> action)
        {
            var succeeded = new List<Guid>();
            var failed = new List<BulkActionFailureDto>();

            foreach (var id in fileIds.Distinct())
            {
                try
                {
                    await action(id);
                    succeeded.Add(id);
                }
                catch (Exception ex) when (ex is Common.Exceptions.NotFoundException or Common.Exceptions.ForbiddenException
                    or Common.Exceptions.ValidationException or Common.Exceptions.ConflictException)
                {
                    failed.Add(new BulkActionFailureDto { FileId = id, Error = ex.Message });
                }
            }

            return new BulkActionResultDto { SucceededIds = succeeded, Failed = failed };
        }
    }
}
