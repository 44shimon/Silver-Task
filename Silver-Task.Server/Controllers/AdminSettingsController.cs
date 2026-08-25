using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Settings;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Administrator-only system configuration — every value here is validated and
    /// actually enforced somewhere in the app (see the individual services this settings store
    /// is injected into); nothing here is a display-only/decorative setting.</summary>
    [ApiController]
    [Route("api/admin/settings")]
    [Authorize(Roles = nameof(Models.Entities.Enums.UserRole.Administrator))]
    public class AdminSettingsController(ISystemSettingsService settingsService) : ControllerBase
    {
        private readonly ISystemSettingsService _settingsService = settingsService;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SystemSettingDto>>> GetAll()
        {
            var settings = await _settingsService.GetAllAsync();
            return Ok(settings.Select(s => s.ToDto()));
        }

        [HttpPut]
        public async Task<ActionResult<IReadOnlyList<SystemSettingDto>>> Update([FromBody] UpdateSystemSettingsRequest request)
        {
            await _settingsService.UpdateAsync(User.GetUserId(), request.Values);
            var settings = await _settingsService.GetAllAsync();
            return Ok(settings.Select(s => s.ToDto()));
        }
    }
}
