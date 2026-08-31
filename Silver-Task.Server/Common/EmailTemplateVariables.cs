namespace Silver_Task.Server.Common
{
    /// <summary>Phase 45 — the fixed, controlled set of {{Variable}} tokens an admin-authored
    /// email template (EmailTemplate) is allowed to reference (spec's own "Template Variables"
    /// section). Nothing outside EmailTemplateService.Substitute ever interprets a template
    /// string, and that method only ever does literal, non-executing text replacement against
    /// this exact property set — there is no path from a template's text to arbitrary code,
    /// SQL, or shell execution.</summary>
    public record EmailTemplateVariables(
        string UserName,
        string ActorName,
        string? TaskName,
        string? ProjectName,
        string? DueDate,
        string? ActionUrl)
    {
        public IReadOnlyDictionary<string, string> ToDictionary() => new Dictionary<string, string>
        {
            ["UserName"] = UserName,
            ["ActorName"] = ActorName,
            ["TaskName"] = TaskName ?? "",
            ["ProjectName"] = ProjectName ?? "",
            ["DueDate"] = DueDate ?? "",
            ["ActionUrl"] = ActionUrl ?? ""
        };
    }
}
