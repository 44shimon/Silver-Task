using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Models.DTOs.FileCategories;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Admin -> File Categories — create/rename/deactivate/delete the shared global
    /// category vocabulary.</summary>
    [ApiController]
    [Route("api/admin/file-categories")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public class AdminFileCategoriesController(IFileCategoryService fileCategoryService) : ControllerBase
    {
        private readonly IFileCategoryService _fileCategoryService = fileCategoryService;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<FileCategoryDto>>> GetAll()
        {
            var categories = await _fileCategoryService.GetAllForAdminAsync();
            return Ok(categories.Select(c => c.ToDto()));
        }

        [HttpPost]
        public async Task<ActionResult<FileCategoryDto>> Create([FromBody] SaveFileCategoryRequest request)
        {
            var category = await _fileCategoryService.CreateAsync(request.Name, request.Description);
            return CreatedAtAction(nameof(GetAll), category.ToDto());
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<FileCategoryDto>> Update(Guid id, [FromBody] SaveFileCategoryRequest request)
        {
            var category = await _fileCategoryService.UpdateAsync(id, request.Name, request.Description);
            return Ok(category.ToDto());
        }

        [HttpPost("{id:guid}/deactivate")]
        public async Task<ActionResult<FileCategoryDto>> Deactivate(Guid id)
        {
            var category = await _fileCategoryService.SetActiveAsync(id, false);
            return Ok(category.ToDto());
        }

        [HttpPost("{id:guid}/activate")]
        public async Task<ActionResult<FileCategoryDto>> Activate(Guid id)
        {
            var category = await _fileCategoryService.SetActiveAsync(id, true);
            return Ok(category.ToDto());
        }

        /// <summary>Refuses (409) if the category is still applied to any file — see
        /// IFileCategoryService.DeleteAsync's own doc comment.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _fileCategoryService.DeleteAsync(id);
            return NoContent();
        }
    }
}
