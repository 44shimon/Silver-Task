using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Folders;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/folders")]
    public class FoldersController(IFolderService folderService) : ControllerBase
    {
        private readonly IFolderService _folderService = folderService;

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<FolderDto>> GetById(Guid id)
        {
            var folder = await _folderService.GetByIdAsync(id, User.GetUserId(), User.GetRole());
            return Ok(folder.ToDto());
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<FolderDto>> Rename(Guid id, [FromBody] RenameFolderRequest request)
        {
            var folder = await _folderService.RenameAsync(id, request.Name, User.GetUserId(), User.GetRole());
            return Ok(folder.ToDto());
        }

        [HttpPost("{id:guid}/move")]
        public async Task<ActionResult<FolderDto>> Move(Guid id, [FromBody] MoveFolderRequest request)
        {
            var folder = await _folderService.MoveAsync(id, request.ParentFolderId, User.GetUserId(), User.GetRole());
            return Ok(folder.ToDto());
        }

        /// <summary>Backs the "This folder contains N files and M subfolders" confirmation —
        /// fetched by the frontend before showing the delete dialog's options.</summary>
        [HttpGet("{id:guid}/delete-preview")]
        public async Task<ActionResult<FolderDeletePreviewDto>> GetDeletePreview(Guid id)
        {
            var (fileCount, subfolderCount) = await _folderService.GetDeletePreviewAsync(id, User.GetUserId(), User.GetRole());
            return Ok(new FolderDeletePreviewDto { FileCount = fileCount, SubfolderCount = subfolderCount });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, [FromQuery] FolderDeleteMode mode = FolderDeleteMode.MoveContentsToParent)
        {
            await _folderService.DeleteAsync(id, mode, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpPost("{id:guid}/restore")]
        public async Task<ActionResult<FolderDto>> Restore(Guid id)
        {
            var folder = await _folderService.RestoreAsync(id, User.GetUserId(), User.GetRole());
            return Ok(folder.ToDto());
        }
    }
}
