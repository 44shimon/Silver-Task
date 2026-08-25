using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Data;

namespace Silver_Task.Server.Middleware
{
    /// <summary>
    /// JWTs are stateless and carry no server-side session record, so deactivating or deleting a
    /// user (Phase 26) can't revoke an already-issued token the usual way. This is the
    /// alternative the architecture *does* support: re-check the caller's current IsActive state
    /// against the database on every authenticated request (after UseAuthentication, before
    /// UseAuthorization) and reject with 401 the moment it goes false — capping the exposure
    /// window to "next request" instead of "until the token's own expiry". A deleted user always
    /// has IsActive=false too (see UserService.DeleteAsync), so a single check covers both.
    ///
    /// Skips [AllowAnonymous] endpoints (relies on endpoint routing having already resolved
    /// context.GetEndpoint() by this point in the pipeline, which is automatic in the minimal
    /// hosting model) — a deactivated user holding a still-unexpired cookie who calls, say,
    /// POST /api/auth/login must still reach AuthController's own logic (which returns a clean
    /// "invalid email or password" and would let a *re*-activated user log back in), not get a
    /// blank 401 from this middleware before the endpoint ever runs.
    /// </summary>
    public class ActiveUserMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context, AppDbContext db)
        {
            var allowsAnonymous = context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;

            if (!allowsAnonymous && context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.GetUserId();
                var isActive = await db.Users.Where(u => u.Id == userId).Select(u => u.IsActive).FirstOrDefaultAsync();
                if (!isActive)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }

            await _next(context);
        }
    }
}
