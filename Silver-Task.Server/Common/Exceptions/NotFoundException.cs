namespace Silver_Task.Server.Common.Exceptions
{
    /// <summary>Thrown by services when a requested resource does not exist. Maps to HTTP 404.</summary>
    public class NotFoundException(string message) : Exception(message);
}
