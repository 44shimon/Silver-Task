using Silver_Task.Server.Models.DTOs.SavedViews;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Common
{
    /// <summary>Phase 43 — which list a SavedView filters/renders. Mirrors TemplateTypes' own
    /// "plain string-constants file, not a third enum" pattern. A view's EntityType is immutable
    /// after creation (same convention as CustomField.FieldType/EntityType).</summary>
    public static class SavedViewEntityTypes
    {
        public const string Task = "Task";
        public const string Project = "Project";

        public static readonly string[] All = [Task, Project];
    }

    /// <summary>Phase 43 — display layout for a Task-entity SavedView. Table is the only layout
    /// that works for a view spanning more than one project (Kanban/Calendar/Timeline/Gantt are
    /// all single-project components per their own existing props — see SavedViewService's own
    /// doc comment on ResolvedSingleProjectId). Not applicable to Project-entity views, which are
    /// always rendered as a table.</summary>
    public static class SavedViewLayouts
    {
        public const string Table = "Table";
        public const string Kanban = "Kanban";
        public const string Calendar = "Calendar";
        public const string Timeline = "Timeline";
        public const string Gantt = "Gantt";

        public static readonly string[] All = [Table, Kanban, Calendar, Timeline, Gantt];
    }

    /// <summary>Phase 43 — the built-in (non-custom-field) filterable fields a SavedView's filter
    /// tree can reference. A condition's Field is one of these OR "customField:{guid}" — see
    /// SavedViewFilterConditionDto's own doc comment. Kept as a closed constant set (not raw
    /// strings) so SavedViewFilterValidator can reject anything else up front, same reasoning as
    /// AutomationConditionOperator being a closed enum.</summary>
    public static class SavedViewFields
    {
        public const string Status = "status";
        public const string Priority = "priority";
        public const string AssigneeId = "assigneeId";
        public const string ProjectId = "projectId";
        public const string TagId = "tagId";
        public const string DueDate = "dueDate";
        public const string CreatedAt = "createdAt";
        public const string UpdatedAt = "updatedAt";

        public static readonly string[] TaskFields = [Status, Priority, AssigneeId, ProjectId, TagId, DueDate, CreatedAt, UpdatedAt];

        public const string CustomFieldPrefix = "customField:";

        /// <summary>Sentinel assignee value resolved to the CALLER's own id fresh on every
        /// execution — never persisted as a literal user Guid at save time (spec's own explicit
        /// "do not store a hard-coded user ID when the view is created").</summary>
        public const string AssigneeMe = "me";

        /// <summary>Sentinel assignee value matching AssignedToUserId == null.</summary>
        public const string AssigneeUnassigned = "unassigned";
    }

    /// <summary>Phase 43 — relative date tokens (spec #21). Resolved to concrete date bounds fresh
    /// on every execution inside SavedViewFilterEngine, never converted to fixed dates at save
    /// time, so a saved "Due This Week" view keeps meaning "this week" indefinitely.</summary>
    public static class SavedViewRelativeDates
    {
        public const string Today = "today";
        public const string Tomorrow = "tomorrow";
        public const string ThisWeek = "thisWeek";
        public const string NextWeek = "nextWeek";
        public const string ThisMonth = "thisMonth";
        public const string Overdue = "overdue";
        public const string NoDueDate = "noDueDate";

        public static readonly string[] All = [Today, Tomorrow, ThisWeek, NextWeek, ThisMonth, Overdue, NoDueDate];
    }

    /// <summary>Phase 43 — the six built-in default views (spec's own explicit list). Represented
    /// as VIRTUAL views identified by fixed, well-known GUIDs rather than real, per-user-seeded
    /// SavedView rows: SavedViewService synthesizes them into every list response, and recognizes
    /// a well-known id at execute/favorite time to run the hardcoded definition below instead of a
    /// DB lookup. This sidesteps per-user seeding/migration complexity entirely and trivially
    /// avoids ever having a "duplicate" default view, since nothing is stored to duplicate. Each
    /// definition's Filter reuses the exact same recursive DSL a user-created view uses — a system
    /// default is not a special case inside the filter engine, only in how/where it's looked up.</summary>
    public static class SavedViewSystemDefaults
    {
        public static readonly Guid MyTasksId = new("00000000-0000-0000-0000-000000000001");
        public static readonly Guid OverdueId = new("00000000-0000-0000-0000-000000000002");
        public static readonly Guid DueTodayId = new("00000000-0000-0000-0000-000000000003");
        public static readonly Guid DueThisWeekId = new("00000000-0000-0000-0000-000000000004");
        public static readonly Guid RecentlyUpdatedId = new("00000000-0000-0000-0000-000000000005");
        public static readonly Guid UnassignedId = new("00000000-0000-0000-0000-000000000006");

        public sealed record Definition(Guid Id, string Name, string Description, SavedViewFilterGroupDto Filter, string? SortField, bool SortDescending);

        public static IReadOnlyList<Definition> All { get; } =
        [
            new(MyTasksId, "My Tasks", "Every task assigned to you across all your projects.",
                Condition(SavedViewFields.AssigneeId, AutomationConditionOperator.Equals, SavedViewFields.AssigneeMe), "dueDate", false),
            new(OverdueId, "Overdue", "Open tasks whose due date has passed.",
                Condition(SavedViewFields.DueDate, AutomationConditionOperator.Equals, SavedViewRelativeDates.Overdue), "dueDate", false),
            new(DueTodayId, "Due Today", "Tasks due today.",
                Condition(SavedViewFields.DueDate, AutomationConditionOperator.Equals, SavedViewRelativeDates.Today), "dueDate", false),
            new(DueThisWeekId, "Due This Week", "Tasks due this week.",
                Condition(SavedViewFields.DueDate, AutomationConditionOperator.Equals, SavedViewRelativeDates.ThisWeek), "dueDate", false),
            new(RecentlyUpdatedId, "Recently Updated", "Every accessible task, newest update first.",
                new SavedViewFilterGroupDto(), "updatedAt", true),
            new(UnassignedId, "Unassigned", "Tasks with no assignee.",
                Condition(SavedViewFields.AssigneeId, AutomationConditionOperator.Equals, SavedViewFields.AssigneeUnassigned), "createdAt", true)
        ];

        public static Definition? Find(Guid id) => All.FirstOrDefault(d => d.Id == id);

        private static SavedViewFilterGroupDto Condition(string field, AutomationConditionOperator op, string value) => new()
        {
            Logic = "AND",
            Conditions = [new SavedViewFilterConditionDto { Field = field, Operator = op, Value = value }]
        };
    }
}
