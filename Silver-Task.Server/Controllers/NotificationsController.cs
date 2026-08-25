using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Notifications;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>
    /// Every action here reads the target user from User.GetUserId() (the authenticated
    /// caller's own id) — none of them ever accept a user id from the request, the same
    /// self-scoping pattern UserSettingsController already established, so there is no way to
    /// reach another user's notifications through this controller. There is deliberately no
    /// admin/system-wide notification endpoint here; if that's ever needed it's a separate
    /// feature, not a normal-user route with a caller-supplied user id.
    /// </summary>
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController(INotificationService notificationService) : ControllerBase
    {
        private readonly INotificationService _notificationService = notificationService;

        [HttpGet]
        public async Task<ActionResult<NotificationListDto>> GetAll(
            [FromQuery] bool? isRead,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var (items, totalCount) = await _notificationService.GetForUserAsync(User.GetUserId(), isRead, page, pageSize);
            return Ok(new NotificationListDto
            {
                Items = items.Select(n => n.ToDto()).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        [HttpGet("unread-count")]
        public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
        {
            var count = await _notificationService.GetUnreadCountAsync(User.GetUserId());
            return Ok(new UnreadCountDto { Count = count });
        }

        [HttpPut("{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            await _notificationService.MarkReadAsync(id, User.GetUserId());
            return NoContent();
        }

        [HttpPut("{id:guid}/unread")]
        public async Task<IActionResult> MarkUnread(Guid id)
        {
            await _notificationService.MarkUnreadAsync(id, User.GetUserId());
            return NoContent();
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            await _notificationService.MarkAllReadAsync(User.GetUserId());
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _notificationService.DeleteAsync(id, User.GetUserId());
            return NoContent();
        }
    }
}
