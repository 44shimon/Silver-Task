namespace Silver_Task.Server.Models.DTOs.Automations
{
    public class TestAutomationRequest
    {
        /// <summary>The task/file/project id to evaluate this automation's conditions against —
        /// which entity type depends on the automation's own TriggerType.</summary>
        public Guid SampleEntityId { get; set; }
    }
}
