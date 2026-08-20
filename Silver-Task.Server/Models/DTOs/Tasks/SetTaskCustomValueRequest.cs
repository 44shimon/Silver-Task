namespace Silver_Task.Server.Models.DTOs.Tasks
{
    public class SetTaskCustomValueRequest
    {
        /// <summary>Null or empty clears the value.</summary>
        public string? Value { get; set; }
    }
}
