using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Models.DTOs.FileCategories;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Global, read-only category picker — any authenticated user (categories aren't
    /// project-scoped). Creating/renaming/deactivating/deleting is Administrator-only, see
    /// AdminFileCategoriesController.</summary>
    [ApiController]
    [Route("api/file-categories")]
    public class FileCategoriesController(IFileCategoryService fileCategoryService) : ControllerBase
    {
        private readonly IFileCategoryService _fileCategoryService = fileCategoryService;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<FileCategoryDto>>> GetActive()
        {
            var categories = await _fileCategoryService.GetActiveAsync();
            return Ok(categories.Select(c => c.ToDto()));
        }
    }
}
