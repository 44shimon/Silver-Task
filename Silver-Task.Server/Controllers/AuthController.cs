using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Auth;
using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(IAuthService authService, IUserService userService, IPermissionService permissionService) : ControllerBase
    {
        private readonly IAuthService _authService = authService;
        private readonly IUserService _userService = userService;
        private readonly IPermissionService _permissionService = permissionService;

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<UserDto>> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request.Email, request.Password);
            if (result is null)
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            var (user, token, expiresAtUtc) = result.Value;
            SetAuthCookie(token, expiresAtUtc);

            var permissions = await _permissionService.GetSystemPermissionsAsync(user.Role);
            return Ok(user.ToDto([.. permissions]));
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete(AuthCookie.Name, new CookieOptions { Path = "/" });
            return NoContent();
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> Me()
        {
            var user = await _userService.GetByIdAsync(User.GetUserId());
            var permissions = await _permissionService.GetSystemPermissionsAsync(user.Role);
            return Ok(user.ToDto([.. permissions]));
        }

        private void SetAuthCookie(string token, DateTime expiresAtUtc)
        {
            Response.Cookies.Append(AuthCookie.Name, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = expiresAtUtc,
                Path = "/"
            });
        }
    }
}
