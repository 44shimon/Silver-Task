using System.Net;
using System.Text.Json;
using Silver_Task.Server.Models.Common;

namespace Silver_Task.Server.Middleware
{
    /// <summary>
    /// Catches unhandled exceptions so callers always receive a clean JSON error
    /// instead of a stack trace, regardless of environment.
    /// </summary>
    public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger;

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                var response = new ApiErrorResponse
                {
                    Message = "An unexpected error occurred. Please try again later.",
                    TraceId = context.TraceIdentifier
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            }
        }
    }
}
