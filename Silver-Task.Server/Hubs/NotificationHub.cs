using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Silver_Task.Server.Common;

namespace Silver_Task.Server.Hubs
{
    /// <summary>
    /// Phase 36 real-time notification push — this app had no SignalR/WebSocket infrastructure
    /// before this phase (confirmed by inspection: the notification bell previously relied purely
    /// on a 60s poll). Added because the JWT-in-httpOnly-cookie auth this app already uses (see
    /// Program.cs's JwtBearerEvents.OnMessageReceived) authorizes a SignalR connection automatically
    /// with zero extra plumbing — the browser sends the same cookie on the hub's negotiate/websocket
    /// handshake it sends on every other same-origin request, so [Authorize] here works exactly
    /// like it does on any controller.
    ///
    /// Deliberately thin: this hub has no client-callable methods at all. Its only job is to put
    /// each connection into a per-user group on connect, so NotificationPushInterceptor (see
    /// Data/) can target "this one user" when a Notification row commits. The existing 60s poll in
    /// useUnreadCount stays in place as the offline-resilience fallback (per the spec's own "do
    /// not rely exclusively on WebSockets" instruction) — this hub only makes the common case
    /// (browser tab open, connection alive) feel instant instead of up-to-60s stale.
    /// </summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        public static string GroupName(Guid userId) => $"user:{userId}";

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(Context.User!.GetUserId()));
            await base.OnConnectedAsync();
        }
    }
}
