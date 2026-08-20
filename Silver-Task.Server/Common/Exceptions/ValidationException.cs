namespace Silver_Task.Server.Common.Exceptions
{
    /// <summary>Thrown by services for business-rule validation that DataAnnotations can't express (e.g. cross-entity rules). Maps to HTTP 400.</summary>
    public class ValidationException(string message) : Exception(message);
}
