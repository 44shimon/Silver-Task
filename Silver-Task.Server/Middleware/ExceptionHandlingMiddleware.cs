using System.Net;
using System.Text.Json;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Models.Common;

namespace Silver_Task.Server.Middleware
{
    /// <summary>
    /// Catches unhandled exceptions so callers always receive a clean JSON error
    /// instead of a stack trace, regardless of environment. Known domain exceptions map
    /// to their corresponding HTTP status; anything else becomes a generic 500.
    /// </summary>
    public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var (statusCode, message, logAsError) = ex switch
                {
                    NotFoundException => (HttpStatusCode.NotFound, ex.Message, false),
                    DependencyBlockedException => (HttpStatusCode.Conflict, ex.Message, false),
                    ConflictException => (HttpStatusCode.Conflict, ex.Message, false),
                    ForbiddenException => (HttpStatusCode.Forbidden, ex.Message, false),
                    ValidationException => (HttpStatusCode.BadRequest, ex.Message, false),
                    _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.", true)
                };

                // Phase 49 — TraceId included in the log line itself (not just returned to the
                // client), so an operator can actually grep for the TraceId a user reports and
                // find the matching log entry instead of falling back to approximate
                // correlation by timestamp + method/path.
                if (logAsError)
                {
                    _logger.LogError(ex, "Unhandled exception processing {Method} {Path} (TraceId: {TraceId})", context.Request.Method, context.Request.Path, context.TraceIdentifier);
                }
                else
                {
                    _logger.LogInformation("{ExceptionType} handling {Method} {Path}: {Message} (TraceId: {TraceId})", ex.GetType().Name, context.Request.Method, context.Request.Path, ex.Message, context.TraceIdentifier);
                }

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)statusCode;

                var response = new ApiErrorResponse
                {
                    Message = message,
                    TraceId = context.TraceIdentifier,
                    Errors = ex is DependencyBlockedException blocked
                        ? new Dictionary<string, string[]> { ["blockedBy"] = [.. blocked.BlockingTaskTitles] }
                        : null
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
            }
        }
    }
}
