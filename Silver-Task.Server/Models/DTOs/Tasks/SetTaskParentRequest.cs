namespace Silver_Task.Server.Models.DTOs.Tasks
{
    /// <summary>Null ParentTaskId moves the task to top level.</summary>
    public class SetTaskParentRequest
    {
        public Guid? ParentTaskId { get; set; }
    }
}
