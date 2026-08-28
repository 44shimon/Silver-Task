using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.SavedViews
{
    /// <summary>Phase 43 — one leaf comparison in a saved view's filter tree. Field is either a
    /// SavedViewFields constant or "customField:{guid}" (Phase 41 integration). Never
    /// interpreted as SQL text — every leaf is resolved by SavedViewFilterEngine into one
    /// targeted, parameterized query. ValueTo is only consulted for the Between operator; Value
    /// alone can also carry a SavedViewRelativeDates token (e.g. "overdue") for date fields
    /// instead of a literal ISO date, resolved fresh on every execution.</summary>
    public class SavedViewFilterConditionDto
    {
        public required string Field { get; set; }

        public AutomationConditionOperator Operator { get; set; }

        public string? Value { get; set; }

        public string? ValueTo { get; set; }
    }

    /// <summary>A node in the recursive AND/OR filter tree (spec #36/#37 — nested/mixed groups).
    /// Logic is "AND" or "OR"; a group with neither Conditions nor Groups vacuously matches
    /// everything (an empty filter = no restriction).</summary>
    public class SavedViewFilterGroupDto
    {
        public string Logic { get; set; } = "AND";

        public List<SavedViewFilterConditionDto> Conditions { get; set; } = [];

        public List<SavedViewFilterGroupDto> Groups { get; set; } = [];
    }
}
