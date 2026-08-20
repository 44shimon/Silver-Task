using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Models.DTOs.Users;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public class UsersController(IUserService userService) : ControllerBase
    {
        private readonly IUserService _userService = userService;

        /// <summary>
        /// Creates a user. Requires an authenticated Administrator — except when the system
        /// has no users at all yet, in which case this is open so the first account can be
        /// created; that account is always made an Administrator.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request)
        {
            var usersExist = await _userService.AnyUsersExistAsync();
            if (usersExist)
            {
                if (User.Identity?.IsAuthenticated != true)
                {
                    return Unauthorized();
                }

                if (!User.IsInRole(nameof(UserRole.Administrator)))
                {
                    return Forbid();
                }
            }

            var user = await _userService.CreateAsync(request, isBootstrap: !usersExist);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user.ToDto());
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll()
        {
            var users = await _userService.GetAllAsync();
            return Ok(users.Select(u => u.ToDto()));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UserDto>> GetById(Guid id)
        {
            var user = await _userService.GetByIdAsync(id);
            return Ok(user.ToDto());
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest request)
        {
            var user = await _userService.UpdateAsync(id, request);
            return Ok(user.ToDto());
        }
    }
}
