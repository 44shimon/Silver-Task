namespace Silver_Task.Server.Common.Exceptions
{
    /// <summary>Phase 39 — thrown when a task's Status can't change because one or more
    /// dependency rules aren't satisfied (e.g. completing a task whose Finish-to-Finish
    /// prerequisite isn't Complete yet). Distinct from plain ValidationException specifically so
    /// ExceptionHandlingMiddleware can attach the structured blocker list
    /// (ApiErrorResponse.Errors["blockedBy"]) the frontend needs to offer an Override Dependency
    /// action — a generic 400 message alone wouldn't let the UI distinguish "blocked by a
    /// dependency" from any other validation failure. Maps to HTTP 409 (Conflict), matching
    /// ConflictException's existing semantics for "the request conflicts with current state."</summary>
    public class DependencyBlockedException(string message, IReadOnlyList<string> blockingTaskTitles) : Exception(message)
    {
        public IReadOnlyList<string> BlockingTaskTitles { get; } = blockingTaskTitles;
    }
}
