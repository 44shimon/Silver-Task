using System.Globalization;
using System.Text.RegularExpressions;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Common.Automation
{
    /// <summary>Everything a template string might reference — always fully pre-loaded by the
    /// caller (AutomationService already has the task/project loaded to evaluate conditions with,
    /// so resolving variables costs no extra queries, per the spec's own "without unnecessary
    /// database queries" guidance on event payloads, applied here too).</summary>
    public class AutomationVariableContext
    {
        public TaskItem? Task { get; init; }

        /// <summary>The identity a template's {{user.name}} resolves to — always the automation's
        /// own creator (the identity every action executes as), not "whoever triggered the
        /// event", since several triggers (TaskOverdue) have no such actor at all. See
        /// AutomationService's own doc comment on the execution-identity model.</summary>
        public User? ActingUser { get; init; }
    }

    /// <summary>Resolves the small, fixed set of {{...}} placeholders the spec allows inside
    /// automation title/description/comment templates — a plain regex + switch over known keys,
    /// never an expression evaluator or anything resembling eval(); an unrecognized variable
    /// silently becomes an empty string rather than erroring, so a typo in a template can't break
    /// task creation/commenting. See the spec's own "no script execution" requirement.</summary>
    public interface IAutomationVariableResolver
    {
        string Resolve(string template, AutomationVariableContext context);
    }

    public class AutomationVariableResolver : IAutomationVariableResolver
    {
        private static readonly Regex VariablePattern = new(@"\{\{\s*([a-zA-Z0-9_.]+)\s*\}\}", RegexOptions.Compiled);

        public string Resolve(string template, AutomationVariableContext context) =>
            VariablePattern.Replace(template, match => ResolveOne(match.Groups[1].Value, context) ?? string.Empty);

        private static string? ResolveOne(string key, AutomationVariableContext context) => key.ToLowerInvariant() switch
        {
            "task.title" => context.Task?.Title,
            "task.description" => context.Task?.Description,
            "task.id" => context.Task?.Id.ToString(),
            "task.assignee" => context.Task?.AssignedTo?.Name,
            "task.project" => context.Task?.Project?.Name,
            "task.duedate" => context.Task?.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "user.name" => context.ActingUser?.Name,
            _ => null
        };
    }
}
