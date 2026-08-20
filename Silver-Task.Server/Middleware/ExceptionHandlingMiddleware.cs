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
                    ConflictException => (HttpStatusCode.Conflict, ex.Message, false),
                    ForbiddenException => (HttpStatusCode.Forbidden, ex.Message, false),
                    ValidationException => (HttpStatusCode.BadRequest, ex.Message, false),
                    _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.", true)
                };

                if (logAsError)
                {
                    _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
                }
                else
                {
                    _logger.LogInformation("{ExceptionType} handling {Method} {Path}: {Message}", ex.GetType().Name, context.Request.Method, context.Request.Path, ex.Message);
                }

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)statusCode;

                var response = new ApiErrorResponse
                {
                    Message = message,
                    TraceId = context.TraceIdentifier
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
            }
        }
    }
}
