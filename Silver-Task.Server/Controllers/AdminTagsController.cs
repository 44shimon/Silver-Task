using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Models.DTOs.Tags;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Admin -> Tags — rename/deactivate/delete the shared global tag vocabulary.
    /// Ad-hoc tag creation while tagging a file stays in AttachmentsController (get-or-create);
    /// this controller only manages the definitions themselves.</summary>
    [ApiController]
    [Route("api/admin/tags")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public class AdminTagsController(ITagService tagService) : ControllerBase
    {
        private readonly ITagService _tagService = tagService;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TagDto>>> GetAll()
        {
            var tags = await _tagService.GetAllForAdminAsync();
            return Ok(tags.Select(t => t.ToDto()));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<TagDto>> Rename(Guid id, [FromBody] UpdateTagRequest request)
        {
            var tag = await _tagService.RenameAsync(id, request.Name);
            return Ok(tag.ToDto());
        }

        [HttpPost("{id:guid}/deactivate")]
        public async Task<ActionResult<TagDto>> Deactivate(Guid id)
        {
            var tag = await _tagService.SetActiveAsync(id, false);
            return Ok(tag.ToDto());
        }

        [HttpPost("{id:guid}/activate")]
        public async Task<ActionResult<TagDto>> Activate(Guid id)
        {
            var tag = await _tagService.SetActiveAsync(id, true);
            return Ok(tag.ToDto());
        }

        /// <summary>Refuses (409) if the tag is still applied to any file — see
        /// ITagService.DeleteAsync's own doc comment.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _tagService.DeleteAsync(id);
            return NoContent();
        }
    }
}
