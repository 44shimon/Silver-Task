using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Notifications;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>
    /// Every action here reads the target user from User.GetUserId() (the authenticated
    /// caller's own id) — none of them ever accept a user id from the request, the same
    /// self-scoping pattern UserSettingsController already established, so there is no way to
    /// reach another user's notifications through this controller regardless of what a caller
    /// sends (e.g. a query-string userId is simply not a parameter that exists here — see
    /// GetAll's own signature). There is deliberately no admin/system-wide notification endpoint
    /// here; if that's ever needed it's a separate feature, not a normal-user route with a
    /// caller-supplied user id.
    ///
    /// Deep links (NotificationDto.ActionUrl) point at ordinary app routes (e.g.
    /// /projects/{id}?task={id}), which independently re-enforce authorization on load through
    /// the normal Tasks/Projects controllers — a notification about a task/project the caller has
    /// since lost access to (removed from the project, task deleted) can never be used to bypass
    /// that; the destination route 403s/404s exactly as it would for any other direct navigation.
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
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? type = null,
            [FromQuery] string? category = null,
            [FromQuery] NotificationPriority? priority = null,
            [FromQuery] Guid? projectId = null,
            [FromQuery] Guid? taskId = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            var (items, totalCount) = await _notificationService.GetForUserAsync(
                User.GetUserId(), isRead, page, pageSize, search, type, priority, projectId, taskId, dateFrom, dateTo,
                NotificationCategories.Resolve(category));
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

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<NotificationDto>> GetById(Guid id)
        {
            var notification = await _notificationService.GetByIdAsync(id, User.GetUserId());
            return Ok(notification.ToDto());
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

        [HttpPost("bulk/read")]
        public async Task<IActionResult> BulkMarkRead([FromBody] BulkNotificationActionRequest request)
        {
            await _notificationService.BulkMarkReadAsync(request.Ids, User.GetUserId());
            return NoContent();
        }

        [HttpPost("bulk/dismiss")]
        public async Task<IActionResult> BulkDismiss([FromBody] BulkNotificationActionRequest request)
        {
            await _notificationService.BulkDeleteAsync(request.Ids, User.GetUserId());
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
