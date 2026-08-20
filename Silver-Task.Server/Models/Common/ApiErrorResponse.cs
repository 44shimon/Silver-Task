namespace Silver_Task.Server.Models.Common
{
    public class ApiErrorResponse
    {
        public required string Message { get; set; }

        public string? TraceId { get; set; }

        public IDictionary<string, string[]>? Errors { get; set; }
    }
}
