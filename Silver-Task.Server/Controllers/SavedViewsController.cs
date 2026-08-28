using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.SavedViews;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>
    /// Phase 43 — CRUD/share/favorite/execute for SavedView. Every action derives the caller from
    /// User.GetUserId()/User.GetRole(); ownership/share-visibility checks live inside
    /// ISavedViewService, and Execute re-validates the caller's LIVE project access every single
    /// call — a view can never grant access beyond the executing caller's own current task/project
    /// permissions, regardless of who created or shared it (the spec's own non-negotiable rule).
    /// </summary>
    [ApiController]
    [Route("api/views")]
    [Authorize]
    public class SavedViewsController(ISavedViewService savedViewService) : ControllerBase
    {
        private readonly ISavedViewService _savedViewService = savedViewService;

        [HttpGet]
        public async Task<ActionResult<List<SavedViewDto>>> List()
        {
            return Ok(await _savedViewService.ListForCallerAsync(User.GetUserId(), User.GetRole()));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SavedViewDto>> GetById(Guid id)
        {
            return Ok(await _savedViewService.GetByIdAsync(id, User.GetUserId(), User.GetRole()));
        }

        [HttpPost]
        public async Task<ActionResult<SavedViewDto>> Create([FromBody] SaveViewRequest request)
        {
            var view = await _savedViewService.CreateAsync(User.GetUserId(), User.GetRole(), request);
            return CreatedAtAction(nameof(GetById), new { id = view.Id }, view);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<SavedViewDto>> Update(Guid id, [FromBody] SaveViewRequest request)
        {
            return Ok(await _savedViewService.UpdateAsync(id, User.GetUserId(), User.GetRole(), request));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _savedViewService.DeleteAsync(id, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpPost("{id:guid}/duplicate")]
        public async Task<ActionResult<SavedViewDto>> Duplicate(Guid id)
        {
            return Ok(await _savedViewService.DuplicateAsync(id, User.GetUserId(), User.GetRole()));
        }

        [HttpPost("{id:guid}/share")]
        public async Task<IActionResult> Share(Guid id, [FromBody] ShareViewRequest request)
        {
            var found = await _savedViewService.ShareAsync(id, User.GetUserId(), User.GetRole(), request.Email);
            if (!found)
            {
                return NotFound(new { message = $"No user found with email '{request.Email}'." });
            }
            return NoContent();
        }

        [HttpDelete("{id:guid}/share/{userId:guid}")]
        public async Task<IActionResult> Unshare(Guid id, Guid userId)
        {
            await _savedViewService.UnshareAsync(id, User.GetUserId(), User.GetRole(), userId);
            return NoContent();
        }

        [HttpPost("{id:guid}/favorite")]
        public async Task<IActionResult> Favorite(Guid id)
        {
            await _savedViewService.FavoriteAsync(id, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpDelete("{id:guid}/favorite")]
        public async Task<IActionResult> Unfavorite(Guid id)
        {
            await _savedViewService.UnfavoriteAsync(id, User.GetUserId());
            return NoContent();
        }

        [HttpPut("favorites/order")]
        public async Task<IActionResult> ReorderFavorites([FromBody] List<Guid> orderedViewIds)
        {
            await _savedViewService.ReorderFavoritesAsync(User.GetUserId(), orderedViewIds);
            return NoContent();
        }

        /// <summary>Server-side paginated execution (spec's own explicit "never load everything
        /// and filter in JS" requirement) — the one place every rendering surface for a view's
        /// results goes through.</summary>
        [HttpGet("{id:guid}/execute")]
        public async Task<ActionResult<ExecuteViewResultDto>> Execute(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            return Ok(await _savedViewService.ExecuteAsync(id, User.GetUserId(), User.GetRole(), page, pageSize));
        }

        /// <summary>Ad-hoc, unsaved-filter preview — backs the lightweight "N matching tasks"
        /// count while a view is being built/edited, before Save. The frontend debounces calls
        /// here; this endpoint itself has no special rate limiting since it's already bounded by
        /// the same server-side filter engine every real execution uses.</summary>
        [HttpPost("preview")]
        public async Task<ActionResult<PreviewResultDto>> Preview([FromBody] PreviewViewRequest request)
        {
            return Ok(await _savedViewService.PreviewAsync(request, User.GetUserId(), User.GetRole()));
        }
    }
}
