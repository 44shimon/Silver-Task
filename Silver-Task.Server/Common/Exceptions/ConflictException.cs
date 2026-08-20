namespace Silver_Task.Server.Common.Exceptions
{
    /// <summary>Thrown by services when a request conflicts with existing state (e.g. a duplicate email). Maps to HTTP 409.</summary>
    public class ConflictException(string message) : Exception(message);
}
