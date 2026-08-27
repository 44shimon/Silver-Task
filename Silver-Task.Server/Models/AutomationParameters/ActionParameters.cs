using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.AutomationParameters
{
    /// <summary>One small parameter record per AutomationActionType, (de)serialized to/from
    /// AutomationAction.ParametersJson — see that entity's own doc comment for why JSON was
    /// chosen over a wide set of mostly-null columns. Every field here is a plain value (a user
    /// id, an enum, a template string) — never a script/expression, per the spec's "no script
    /// execution" requirement; AutomationValidator re-validates every one of these on save.</summary>
    public class AssignTaskParameters
    {
        public AutomationUserSelector AssignMode { get; set; }
        public Guid? TargetUserId { get; set; }
    }

    public class ChangeStatusParameters
    {
        public TaskItemStatus NewStatus { get; set; }
    }

    public class ChangePriorityParameters
    {
        public TaskPriority NewPriority { get; set; }
    }

    public class AddLabelParameters
    {
        public required string TagName { get; set; }
    }

    public class RemoveLabelParameters
    {
        public required string TagName { get; set; }
    }

    /// <summary>Exactly one of OffsetDays/ClearDate should be meaningful — ClearDate=true always
    /// wins if both are somehow set (validated at save time to only allow one).</summary>
    public class SetDueDateParameters
    {
        public int? OffsetDays { get; set; }
        public bool ClearDate { get; set; }
    }

    public class SetStartDateParameters
    {
        public int? OffsetDays { get; set; }
        public bool ClearDate { get; set; }
    }

    public class AddCommentParameters
    {
        public required string CommentTemplate { get; set; }
    }

    public class CreateTaskParameters
    {
        public required string TitleTemplate { get; set; }
        public string? DescriptionTemplate { get; set; }
        public AutomationUserSelector AssignMode { get; set; } = AutomationUserSelector.None;
        public Guid? TargetUserId { get; set; }
        public TaskItemStatus? Status { get; set; }
        public TaskPriority? Priority { get; set; }
        public int? DueDateOffsetDays { get; set; }
    }

    public class SendNotificationParameters
    {
        public AutomationUserSelector RecipientMode { get; set; }
        public Guid? TargetUserId { get; set; }
        public required string MessageTemplate { get; set; }
    }

    public class AddFileTagParameters
    {
        public required string TagName { get; set; }
    }
}
