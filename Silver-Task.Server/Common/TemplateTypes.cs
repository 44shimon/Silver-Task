namespace Silver_Task.Server.Common
{
    /// <summary>The two template types the spec asks for (#3: "do not build unnecessary
    /// complexity" — no third type added). Used only as a display discriminator on
    /// TemplateSummaryDto; ProjectTemplate and TaskTemplate remain distinct entities/tables, this
    /// is not a polymorphic base type.</summary>
    public static class TemplateTypes
    {
        public const string Project = "Project";
        public const string Task = "Task";
    }
}
