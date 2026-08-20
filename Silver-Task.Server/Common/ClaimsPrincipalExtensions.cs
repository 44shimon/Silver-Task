using System.Security.Claims;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Common
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal principal)
        {
            var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (value is null || !Guid.TryParse(value, out var userId))
            {
                throw new InvalidOperationException("The current principal does not have a valid user id claim.");
            }

            return userId;
        }

        public static UserRole GetRole(this ClaimsPrincipal principal)
        {
            var value = principal.FindFirstValue(ClaimTypes.Role);
            if (value is null || !Enum.TryParse<UserRole>(value, out var role))
            {
                throw new InvalidOperationException("The current principal does not have a valid role claim.");
            }

            return role;
        }
    }
}
