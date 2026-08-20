namespace Silver_Task.Server.Common.Exceptions
{
    /// <summary>Thrown by services when an authenticated caller lacks permission for an action. Maps to HTTP 403.</summary>
    public class ForbiddenException(string message) : Exception(message);
}
