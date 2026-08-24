using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Settings;
using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>
    /// Self-service "my own account" settings — deliberately a separate controller from the
    /// Administrator-only UsersController (whose class-level [Authorize(Roles=Administrator)]
    /// can't be selectively relaxed per-action). Every action here reads the target user from
    /// User.GetUserId() (the authenticated caller's own id from their auth cookie) — none of
    /// them ever accept a user id from the request, so there is no way to reach another user's
    /// settings through this controller regardless of what a caller sends.
    /// </summary>
    [ApiController]
    [Route("api/users/me")]
    [Authorize]
    public class UserSettingsController(
        IUserService userService,
        IUserPreferencesService preferencesService,
        IUserNotificationSettingsService notificationSettingsService) : ControllerBase
    {
        private readonly IUserService _userService = userService;
        private readonly IUserPreferencesService _preferencesService = preferencesService;
        private readonly IUserNotificationSettingsService _notificationSettingsService = notificationSettingsService;

        [HttpPut]
        public async Task<ActionResult<UserDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var user = await _userService.UpdateProfileAsync(User.GetUserId(), request);
            return Ok(user.ToDto());
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            await _userService.ChangePasswordAsync(User.GetUserId(), request);
            return NoContent();
        }

        [HttpGet("preferences")]
        public async Task<ActionResult<UserPreferencesDto>> GetPreferences()
        {
            var preferences = await _preferencesService.GetOrCreateAsync(User.GetUserId());
            return Ok(preferences.ToDto());
        }

        [HttpPut("preferences")]
        public async Task<ActionResult<UserPreferencesDto>> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
        {
            var preferences = await _preferencesService.UpdateAsync(User.GetUserId(), request);
            return Ok(preferences.ToDto());
        }

        [HttpGet("notifications")]
        public async Task<ActionResult<IReadOnlyList<NotificationSettingDto>>> GetNotificationSettings()
        {
            var settings = await _notificationSettingsService.GetAllAsync(User.GetUserId());
            return Ok(settings.Select(s => s.ToDto()));
        }

        [HttpPut("notifications")]
        public async Task<ActionResult<IReadOnlyList<NotificationSettingDto>>> UpdateNotificationSettings(
            [FromBody] UpdateNotificationSettingsRequest request)
        {
            var settings = await _notificationSettingsService.UpdateAsync(User.GetUserId(), request);
            return Ok(settings.Select(s => s.ToDto()));
        }
    }
}
