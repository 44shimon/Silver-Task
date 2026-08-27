using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>A single step an automation performs, in SortOrder — see AutomationActionType's
    /// own doc comment for the closed, non-destructive set of what's possible. Parameters are
    /// stored as a JSON blob (deserialized per ActionType into one of the small parameter records
    /// in Models/Automation/ActionParameters.cs) rather than a wide set of nullable columns —
    /// every action type needs a different shape (AssignTask needs a user, SetDueDate needs an
    /// offset, CreateTask needs a whole mini task template), and JSON avoids a dozen mostly-null
    /// columns on this table for the sake of a handful of small, fixed, server-validated shapes.
    /// This is not a script/expression field — see AutomationValidator, which parses and
    /// re-validates every field on save; nothing here is ever eval'd.</summary>
    public class AutomationAction
    {
        public Guid Id { get; set; }

        public Guid AutomationId { get; set; }

        public AutomationActionType ActionType { get; set; }

        public required string ParametersJson { get; set; }

        public int SortOrder { get; set; }

        public Automation? Automation { get; set; }
    }
}
