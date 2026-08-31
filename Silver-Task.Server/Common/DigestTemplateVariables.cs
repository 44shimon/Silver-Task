namespace Silver_Task.Server.Common
{
    /// <summary>Phase 46 — the fixed, controlled {{Variable}} allow-list for Daily/Weekly digest
    /// templates (spec's own §60 list exactly) — the digest analog of EmailTemplateVariables.
    /// DigestContent is the one exception to "every value gets HTML-encoded as a whole": it's the
    /// pre-built, already-per-item-encoded section markup (see DigestGenerationService), swapped
    /// in via a literal post-encoding string Replace specifically so admin template text can never
    /// itself inject markup while the (trusted, server-built) digest body still can — see
    /// EmailTemplateService.RenderDigestAsync's own doc comment for exactly how.</summary>
    public record DigestTemplateVariables(
        string UserName,
        string DigestDate,
        int AssignmentCount,
        int MentionCount,
        int CommentCount,
        int DueTodayCount,
        int OverdueCount,
        string ActionUrl)
    {
        public IReadOnlyDictionary<string, string> ToDictionary() => new Dictionary<string, string>
        {
            ["UserName"] = UserName,
            ["DigestDate"] = DigestDate,
            ["AssignmentCount"] = AssignmentCount.ToString(),
            ["MentionCount"] = MentionCount.ToString(),
            ["CommentCount"] = CommentCount.ToString(),
            ["DueTodayCount"] = DueTodayCount.ToString(),
            ["OverdueCount"] = OverdueCount.ToString(),
            ["ActionUrl"] = ActionUrl
            // DigestContent is deliberately excluded — see the class doc comment.
        };
    }
}
