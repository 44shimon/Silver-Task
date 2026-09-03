using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Silver_Task.Server.Data;

namespace Silver_Task.Server.Services
{
    /// <summary>Phase 62 — the "ApiKey" authentication scheme (see Program.cs), reads the
    /// X-Api-Key header. Registered alongside, not instead of, the existing JWT bearer/cookie
    /// scheme — the "ApiKeyOrCookie" policy accepts either, so Controllers/V1/* keeps working for
    /// the SPA's own cookie session exactly as before while also accepting a key.
    ///
    /// On success, builds a ClaimsPrincipal with exactly the claim shape
    /// JwtTokenService.GenerateToken already issues (ClaimTypes.NameIdentifier/Name/Role) from the
    /// key's owning User — so ClaimsPrincipalExtensions.GetUserId()/GetRole() and every downstream
    /// authorization check (ProjectAccessService, [Authorize(Roles=...)]) work completely
    /// unmodified, whether the caller authenticated via cookie or API key. Adds one extra
    /// "auth_method"="apikey" claim so logging/diagnostics can tell the two apart without changing
    /// any authorization decision.
    ///
    /// Never reveals *why* a key was rejected (unknown/revoked/expired/inactive owner) — always
    /// the same generic failure, mirroring AuthService.LoginAsync's own "don't tell the caller
    /// which check failed" precedent.</summary>
    public class ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        AppDbContext db,
        IApiKeyFailureTracker failureTracker,
        IConfiguration configuration)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
    {
        private const string HeaderName = "X-Api-Key";
        private static readonly TimeSpan LastUsedUpdateThrottle = TimeSpan.FromMinutes(1);

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
            {
                return AuthenticateResult.NoResult();
            }

            var presentedKey = headerValues.ToString();
            if (string.IsNullOrWhiteSpace(presentedKey))
            {
                return AuthenticateResult.NoResult();
            }

            var clientIp = Context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var maxFailures = configuration.GetValue("Security:ApiKeyFailureLimit:MaxFailures", 10);
            var windowSeconds = configuration.GetValue("Security:ApiKeyFailureLimit:WindowSeconds", 300);
            if (failureTracker.IsBlocked(clientIp, maxFailures, TimeSpan.FromSeconds(windowSeconds)))
            {
                return AuthenticateResult.Fail("Invalid API key.");
            }

            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey)));
            var key = await db.ApiKeys.Include(k => k.User).FirstOrDefaultAsync(k => k.KeyHash == hash);

            var now = DateTime.UtcNow;
            if (key is null || key.RevokedAt is not null || key.ExpiresAt <= now || key.User is null || !key.User.IsActive)
            {
                failureTracker.RecordFailure(clientIp);
                return AuthenticateResult.Fail("Invalid API key.");
            }

            // Throttled — see ApiKey.LastUsedAt's own doc comment for why this isn't a
            // write-per-request.
            if (key.LastUsedAt is null || now - key.LastUsedAt > LastUsedUpdateThrottle)
            {
                key.LastUsedAt = now;
                await db.SaveChangesAsync();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, key.User.Id.ToString()),
                new(ClaimTypes.Name, key.User.Name),
                new(ClaimTypes.Role, key.User.Role.ToString()),
                new("auth_method", "apikey")
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
    }
}
