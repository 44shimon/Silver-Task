namespace Silver_Task.Server.Common
{
    /// <summary>
    /// The comment system (Phase 11) has never had @mention support or any autocomplete UI, so
    /// there's no existing convention to integrate with — this is a minimal, plain-text one:
    /// typing "@Full Name" (matching a project member's exact display name, case-insensitive)
    /// mentions them. No new comment storage format, no markup — CommentService just scans the
    /// already-stored plain text when deciding who to notify.
    /// </summary>
    public static class MentionParser
    {
        /// <summary>Longest names are matched first so a mention of "Jo Baker" can't be
        /// swallowed by a shorter, coincidentally-prefix-matching member named "Jo".</summary>
        public static IReadOnlyList<Guid> FindMentionedUserIds(string text, IEnumerable<(Guid Id, string Name)> members)
        {
            var matches = new List<Guid>();

            foreach (var (id, name) in members.OrderByDescending(m => m.Name.Length))
            {
                var token = "@" + name;
                var index = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    continue;
                }

                var endIndex = index + token.Length;
                var hasCleanBoundary = endIndex >= text.Length || !char.IsLetterOrDigit(text[endIndex]);
                if (hasCleanBoundary)
                {
                    matches.Add(id);
                }
            }

            return matches;
        }
    }
}
