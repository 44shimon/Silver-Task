using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Models.DTOs.Settings;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>The handful of system settings needed before/without a login session (the login
    /// page's branding). Deliberately separate from AdminSettingsController and deliberately
    /// tiny — see PublicSettingsDto for why nothing else belongs here.</summary>
    [ApiController]
    [Route("api/settings")]
    public class PublicSettingsController(ISystemSettingsService settingsService) : ControllerBase
    {
        private readonly ISystemSettingsService _settingsService = settingsService;

        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<ActionResult<PublicSettingsDto>> GetPublic()
        {
            return Ok(await _settingsService.GetPublicSettingsAsync());
        }
    }
}
