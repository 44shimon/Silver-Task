using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Models.DTOs.Tags;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Global, read-only tag picker — any authenticated user (tags aren't project-scoped).
    /// Creating a new tag happens inline via POST /api/attachments/{id}/tags (get-or-create);
    /// renaming/deactivating/deleting the shared definition is Administrator-only, see
    /// AdminTagsController.</summary>
    [ApiController]
    [Route("api/tags")]
    public class TagsController(ITagService tagService) : ControllerBase
    {
        private readonly ITagService _tagService = tagService;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TagDto>>> GetActive()
        {
            var tags = await _tagService.GetActiveAsync();
            return Ok(tags.Select(t => t.ToDto()));
        }
    }
}
