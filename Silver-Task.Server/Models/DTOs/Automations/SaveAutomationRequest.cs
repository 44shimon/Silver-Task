using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.DTOs.Automations
{
    /// <summary>Full-resource replace for both create and update, matching this API's established
    /// PUT convention — conditions/actions are always sent as a complete replacement list (never a
    /// partial patch), since AutomationService always rebuilds both child collections from
    /// scratch on save (simpler and safer than diffing which condition/action rows to add/remove/
    /// reorder).</summary>
    public class SaveAutomationRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }

        /// <summary>Null = global (Administrator-only, see AutomationService's own doc comment).</summary>
        public Guid? ProjectId { get; set; }

        [Required]
        public AutomationTriggerType TriggerType { get; set; }

        public bool IsActive { get; set; } = true;

        [Required]
        public required List<AutomationConditionRequest> Conditions { get; set; }

        [Required]
        public required List<AutomationActionRequest> Actions { get; set; }
    }

    public class AutomationConditionRequest
    {
        [Required]
        public required string Field { get; set; }

        [Required]
        public AutomationConditionOperator Operator { get; set; }

        public string? Value { get; set; }
    }

    public class AutomationActionRequest
    {
        [Required]
        public AutomationActionType ActionType { get; set; }

        /// <summary>The action's own parameter object (AssignTaskParameters, ChangeStatusParameters,
        /// etc. — see Models/Automation/ActionParameters.cs), sent as a plain JSON object matching
        /// ActionType; AutomationService.ValidateAndSerializeParameters deserializes it against the
        /// exact shape ActionType implies and re-serializes the validated result, so nothing
        /// arbitrary the client sends is ever stored or executed verbatim.</summary>
        [Required]
        public required System.Text.Json.JsonElement Parameters { get; set; }
    }
}
