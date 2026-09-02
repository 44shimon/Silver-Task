namespace Silver_Task.Server.Middleware
{
    /// <summary>
    /// Phase 59 — sets a small, static set of defensive response headers on every response,
    /// including error/maintenance responses (registered right after MaintenanceModeMiddleware, so
    /// even a 503 maintenance response carries them). The CSP is `default-src 'self'` because the
    /// SPA genuinely needs nothing looser: no inline &lt;script&gt;/&lt;style&gt; anywhere in
    /// index.html, no CSS-in-JS library, no external CDN/font references, and the SignalR hub
    /// connects via a same-origin relative URL (`/hubs/notifications`) — confirmed by direct
    /// inspection, not assumed. `frame-ancestors 'none'` plus `X-Frame-Options: DENY` both exist
    /// (the header is the legacy fallback for browsers that don't honor the CSP directive).
    /// </summary>
    public class SecurityHeadersMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        private const string ContentSecurityPolicy =
            "default-src 'self'; connect-src 'self'; img-src 'self' data:; style-src 'self'; " +
            "script-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Content-Security-Policy"] = ContentSecurityPolicy;
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
