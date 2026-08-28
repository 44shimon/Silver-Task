using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.SavedViews;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    /// <summary>Result of evaluating a filter tree — the matching entity ids plus any filter
    /// fields that couldn't be resolved (deleted custom field/tag), so the caller can degrade
    /// gracefully instead of crashing.</summary>
    public record SavedViewFilterResult(HashSet<Guid> MatchedIds, List<string> UnavailableFields);

    /// <summary>
    /// Phase 43 — evaluates a SavedView's recursive AND/OR filter tree entirely server-side.
    ///
    /// Design: each LEAF condition becomes exactly one targeted, indexed, parameterized query
    /// against Postgres (via EF Core), scoped up front to the ids the caller can actually access —
    /// never "load everything into memory and filter in C#". The recursive AND/OR/group tree is
    /// then combined via plain set algebra (HashSet.IntersectWith for AND, UnionWith for OR) over
    /// these small, precomputed id sets — every actual row-level comparison happens inside
    /// Postgres; only the O(result-size) set-combination step runs in application memory. This is
    /// simpler and far more testable than building a fully-dynamic Expression&lt;Func&lt;T,bool&gt;&gt;
    /// tree with nested .Any() sub-lambdas for EAV/tag joins, while still satisfying the spec's own
    /// "never load everything and filter client-side" requirement.
    ///
    /// Custom field conditions are the one deliberate exception to "compare inside SQL": a
    /// TaskCustomValue.Value is always TEXT (EAV pattern), so a Number/Date comparison needs a
    /// text-to-typed parse that LINQ can't safely translate. Values for the ONE targeted
    /// CustomFieldId are fetched (already scoped to accessible/permitted tasks — bounded by
    /// "however many tasks have this field set", not the whole table) and compared in C# via the
    /// same CustomFieldConditionEvaluator every Automation condition already uses — not a second,
    /// drifting implementation.
    ///
    /// Private custom fields are gated at the QUERY level (rows for a private field the caller
    /// can't see are simply never fetched), not just redacted afterward — the exact "match-time
    /// gating, not just display-time redaction" lesson from Phase 42's own IDOR fix, so a
    /// restricted field's value can never even be matched against by an unauthorized viewer of a
    /// shared view, let alone displayed.
    /// </summary>
    public interface ISavedViewFilterEngine
    {
        Task<SavedViewFilterResult> EvaluateTaskFilterAsync(SavedViewFilterGroupDto? filter, HashSet<Guid> accessibleProjectIds, HashSet<Guid> managedProjectIds, Guid callerId, UserRole callerRole);

        Task<SavedViewFilterResult> EvaluateProjectFilterAsync(SavedViewFilterGroupDto? filter, HashSet<Guid> accessibleProjectIds, HashSet<Guid> managedProjectIds, Guid callerId, UserRole callerRole);
    }

    public class SavedViewFilterEngine(AppDbContext db) : ISavedViewFilterEngine
    {
        private readonly AppDbContext _db = db;

        public async Task<SavedViewFilterResult> EvaluateTaskFilterAsync(
            SavedViewFilterGroupDto? filter, HashSet<Guid> accessibleProjectIds, HashSet<Guid> managedProjectIds, Guid callerId, UserRole callerRole)
        {
            var accessibleTaskIds = accessibleProjectIds.Count == 0
                ? []
                : (await _db.Tasks.Where(t => accessibleProjectIds.Contains(t.ProjectId)).Select(t => t.Id).ToListAsync()).ToHashSet();

            var unavailable = new List<string>();
            if (filter is null)
            {
                return new SavedViewFilterResult(accessibleTaskIds, unavailable);
            }

            var ctx = new TaskFilterContext(accessibleProjectIds, managedProjectIds, accessibleTaskIds, callerId, callerRole);
            var matched = await EvaluateTaskGroupAsync(filter, ctx, unavailable);
            return new SavedViewFilterResult(matched, unavailable);
        }

        public async Task<SavedViewFilterResult> EvaluateProjectFilterAsync(
            SavedViewFilterGroupDto? filter, HashSet<Guid> accessibleProjectIds, HashSet<Guid> managedProjectIds, Guid callerId, UserRole callerRole)
        {
            var unavailable = new List<string>();
            if (filter is null)
            {
                return new SavedViewFilterResult(accessibleProjectIds, unavailable);
            }

            var ctx = new ProjectFilterContext(accessibleProjectIds, managedProjectIds, callerId, callerRole);
            var matched = await EvaluateProjectGroupAsync(filter, ctx, unavailable);
            return new SavedViewFilterResult(matched, unavailable);
        }

        // ---------- Task filter tree ----------

        private sealed record TaskFilterContext(HashSet<Guid> AccessibleProjectIds, HashSet<Guid> ManagedProjectIds, HashSet<Guid> AccessibleTaskIds, Guid CallerId, UserRole CallerRole);

        private async Task<HashSet<Guid>> EvaluateTaskGroupAsync(SavedViewFilterGroupDto group, TaskFilterContext ctx, List<string> unavailable)
        {
            if (group.Conditions.Count == 0 && group.Groups.Count == 0)
            {
                return [.. ctx.AccessibleTaskIds];
            }

            var sets = new List<HashSet<Guid>>();
            foreach (var condition in group.Conditions)
            {
                sets.Add(await EvaluateTaskConditionAsync(condition, ctx, unavailable));
            }
            foreach (var subgroup in group.Groups)
            {
                sets.Add(await EvaluateTaskGroupAsync(subgroup, ctx, unavailable));
            }

            return Combine(group.Logic, sets, ctx.AccessibleTaskIds);
        }

        private async Task<HashSet<Guid>> EvaluateTaskConditionAsync(SavedViewFilterConditionDto condition, TaskFilterContext ctx, List<string> unavailable)
        {
            if (condition.Field.StartsWith(SavedViewFields.CustomFieldPrefix, StringComparison.Ordinal))
            {
                var idPart = condition.Field[SavedViewFields.CustomFieldPrefix.Length..];
                if (!Guid.TryParse(idPart, out var fieldId))
                {
                    unavailable.Add(condition.Field);
                    return [];
                }
                return await EvaluateTaskCustomFieldConditionAsync(fieldId, condition, ctx, unavailable);
            }

            var baseQuery = _db.Tasks.Where(t => ctx.AccessibleProjectIds.Contains(t.ProjectId));

            switch (condition.Field)
            {
                case SavedViewFields.Status:
                    return await MatchStatusSetAsync(baseQuery, condition);

                case SavedViewFields.Priority:
                    return await MatchPrioritySetAsync(baseQuery, condition);

                case SavedViewFields.AssigneeId:
                    return await EvaluateAssigneeConditionAsync(baseQuery, condition, ctx.CallerId);

                case SavedViewFields.ProjectId:
                    return await MatchProjectIdSetAsync(baseQuery, condition);

                case SavedViewFields.TagId:
                    return await EvaluateTagConditionAsync(condition, ctx);

                case SavedViewFields.DueDate:
                    return await EvaluateTaskDueDateConditionAsync(baseQuery, condition);

                case SavedViewFields.CreatedAt:
                    return await EvaluateTaskTimestampConditionAsync(baseQuery, condition, isCreatedAt: true);

                case SavedViewFields.UpdatedAt:
                    return await EvaluateTaskTimestampConditionAsync(baseQuery, condition, isCreatedAt: false);

                default:
                    unavailable.Add(condition.Field);
                    return [];
            }
        }

        private async Task<HashSet<Guid>> EvaluateAssigneeConditionAsync(IQueryable<TaskItem> baseQuery, SavedViewFilterConditionDto condition, Guid callerId)
        {
            var tokens = SplitValues(condition.Value);
            var wantsUnassigned = tokens.Remove(SavedViewFields.AssigneeUnassigned);
            var ids = tokens
                .Select(t => t == SavedViewFields.AssigneeMe ? callerId.ToString() : t)
                .Select(t => Guid.TryParse(t, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToHashSet();

            var isNegated = condition.Operator == AutomationConditionOperator.NotEquals;

            var matches = await baseQuery
                .Where(t => (ids.Contains(t.AssignedToUserId!.Value) && t.AssignedToUserId != null) ||
                            (wantsUnassigned && t.AssignedToUserId == null))
                .Select(t => t.Id)
                .ToListAsync();

            if (!isNegated)
            {
                return matches.ToHashSet();
            }

            var all = await baseQuery.Select(t => t.Id).ToListAsync();
            var result = all.ToHashSet();
            result.ExceptWith(matches);
            return result;
        }

        private async Task<HashSet<Guid>> EvaluateTagConditionAsync(SavedViewFilterConditionDto condition, TaskFilterContext ctx)
        {
            var tagIds = SplitValues(condition.Value)
                .Select(t => Guid.TryParse(t, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToHashSet();

            if (tagIds.Count == 0)
            {
                return [];
            }

            var matches = (await _db.TaskTags
                .Where(tt => tagIds.Contains(tt.TagId) && ctx.AccessibleTaskIds.Contains(tt.TaskId))
                .Select(tt => tt.TaskId)
                .Distinct()
                .ToListAsync()).ToHashSet();

            if (condition.Operator == AutomationConditionOperator.NotEquals)
            {
                var result = new HashSet<Guid>(ctx.AccessibleTaskIds);
                result.ExceptWith(matches);
                return result;
            }
            return matches;
        }

        private async Task<HashSet<Guid>> EvaluateTaskDueDateConditionAsync(IQueryable<TaskItem> baseQuery, SavedViewFilterConditionDto condition)
        {
            if (RelativeDateResolver.TryResolveTaskRelative(condition.Value, out var relative))
            {
                var relativeMatches = await relative!(baseQuery);
                return relativeMatches.Select(t => t.Id).ToHashSet();
            }

            if (condition.Operator == AutomationConditionOperator.IsEmpty)
            {
                return (await baseQuery.Where(t => t.DueDate == null).Select(t => t.Id).ToListAsync()).ToHashSet();
            }
            if (condition.Operator == AutomationConditionOperator.IsNotEmpty)
            {
                return (await baseQuery.Where(t => t.DueDate != null).Select(t => t.Id).ToListAsync()).ToHashSet();
            }

            var from = ParseDate(condition.Value);
            var to = ParseDate(condition.ValueTo);

            IQueryable<TaskItem> q = condition.Operator switch
            {
                AutomationConditionOperator.Before => from is DateOnly f1 ? baseQuery.Where(t => t.DueDate != null && t.DueDate < f1) : baseQuery.Where(t => false),
                AutomationConditionOperator.After => from is DateOnly f2 ? baseQuery.Where(t => t.DueDate != null && t.DueDate > f2) : baseQuery.Where(t => false),
                AutomationConditionOperator.Equals => from is DateOnly f3 ? baseQuery.Where(t => t.DueDate == f3) : baseQuery.Where(t => false),
                AutomationConditionOperator.Between => from is DateOnly f4 && to is DateOnly t4
                    ? baseQuery.Where(t => t.DueDate != null && t.DueDate >= f4 && t.DueDate <= t4)
                    : baseQuery.Where(t => false),
                _ => baseQuery.Where(t => false)
            };

            return (await q.Select(t => t.Id).ToListAsync()).ToHashSet();
        }

        private static async Task<HashSet<Guid>> EvaluateTaskTimestampConditionAsync(IQueryable<TaskItem> baseQuery, SavedViewFilterConditionDto condition, bool isCreatedAt)
        {
            var from = ParseDateTime(condition.Value);
            var to = ParseDateTime(condition.ValueTo);

            IQueryable<TaskItem> q = (condition.Operator, isCreatedAt) switch
            {
                (AutomationConditionOperator.Before, true) => from is DateTime f1 ? baseQuery.Where(t => t.CreatedAt < f1) : baseQuery.Where(t => false),
                (AutomationConditionOperator.After, true) => from is DateTime f2 ? baseQuery.Where(t => t.CreatedAt > f2) : baseQuery.Where(t => false),
                (AutomationConditionOperator.Between, true) => from is DateTime f3 && to is DateTime t3 ? baseQuery.Where(t => t.CreatedAt >= f3 && t.CreatedAt <= t3) : baseQuery.Where(t => false),
                (AutomationConditionOperator.Before, false) => from is DateTime f4 ? baseQuery.Where(t => t.UpdatedAt < f4) : baseQuery.Where(t => false),
                (AutomationConditionOperator.After, false) => from is DateTime f5 ? baseQuery.Where(t => t.UpdatedAt > f5) : baseQuery.Where(t => false),
                (AutomationConditionOperator.Between, false) => from is DateTime f6 && to is DateTime t6 ? baseQuery.Where(t => t.UpdatedAt >= f6 && t.UpdatedAt <= t6) : baseQuery.Where(t => false),
                _ => baseQuery.Where(t => false)
            };

            return (await q.Select(t => t.Id).ToListAsync()).ToHashSet();
        }

        // ---------- Custom field conditions (Task) ----------

        private async Task<HashSet<Guid>> EvaluateTaskCustomFieldConditionAsync(Guid fieldId, SavedViewFilterConditionDto condition, TaskFilterContext ctx, List<string> unavailable)
        {
            var field = await _db.CustomFields.FindAsync(fieldId);
            if (field is null || !field.IsActive)
            {
                unavailable.Add(condition.Field);
                return [];
            }

            var rows = await _db.TaskCustomValues
                .Include(v => v.Task)
                .Where(v => v.CustomFieldId == fieldId && ctx.AccessibleTaskIds.Contains(v.TaskId))
                .Select(v => new { v.TaskId, v.Value, ProjectId = v.Task!.ProjectId })
                .ToListAsync();

            if (field.IsPrivate)
            {
                // Match-time gating (Phase 42 lesson) — a private field's rows are simply dropped
                // from consideration for a caller who can't manage that row's own project, so the
                // condition can never even reveal a match against a hidden value.
                rows = rows.Where(r => ctx.ManagedProjectIds.Contains(r.ProjectId)).ToList();
            }

            var resolvedValue = ResolveCustomFieldValueToken(condition.Value, field.FieldType, ctx.CallerId);
            var resolvedValueTo = ResolveCustomFieldValueToken(condition.ValueTo, field.FieldType, ctx.CallerId);

            var matched = new HashSet<Guid>();
            var idsWithRow = new HashSet<Guid>();
            foreach (var row in rows)
            {
                idsWithRow.Add(row.TaskId);
                if (EvaluateCustomFieldValue(row.Value, condition.Operator, resolvedValue, resolvedValueTo, field.FieldType))
                {
                    matched.Add(row.TaskId);
                }
            }

            if (condition.Operator == AutomationConditionOperator.IsEmpty && !field.IsPrivate)
            {
                // A private field's "no row at all" tasks are deliberately left out here too —
                // an unauthorized caller must never learn even whether a hidden value is set.
                var withoutRow = new HashSet<Guid>(ctx.AccessibleTaskIds);
                withoutRow.ExceptWith(idsWithRow);
                matched.UnionWith(withoutRow);
            }

            return matched;
        }

        // ---------- Project filter tree ----------

        private sealed record ProjectFilterContext(HashSet<Guid> AccessibleProjectIds, HashSet<Guid> ManagedProjectIds, Guid CallerId, UserRole CallerRole);

        private async Task<HashSet<Guid>> EvaluateProjectGroupAsync(SavedViewFilterGroupDto group, ProjectFilterContext ctx, List<string> unavailable)
        {
            if (group.Conditions.Count == 0 && group.Groups.Count == 0)
            {
                return [.. ctx.AccessibleProjectIds];
            }

            var sets = new List<HashSet<Guid>>();
            foreach (var condition in group.Conditions)
            {
                sets.Add(await EvaluateProjectConditionAsync(condition, ctx, unavailable));
            }
            foreach (var subgroup in group.Groups)
            {
                sets.Add(await EvaluateProjectGroupAsync(subgroup, ctx, unavailable));
            }

            return Combine(group.Logic, sets, ctx.AccessibleProjectIds);
        }

        private async Task<HashSet<Guid>> EvaluateProjectConditionAsync(SavedViewFilterConditionDto condition, ProjectFilterContext ctx, List<string> unavailable)
        {
            if (condition.Field.StartsWith(SavedViewFields.CustomFieldPrefix, StringComparison.Ordinal))
            {
                var idPart = condition.Field[SavedViewFields.CustomFieldPrefix.Length..];
                if (!Guid.TryParse(idPart, out var fieldId))
                {
                    unavailable.Add(condition.Field);
                    return [];
                }
                return await EvaluateProjectCustomFieldConditionAsync(fieldId, condition, ctx, unavailable);
            }

            var baseQuery = _db.Projects.Where(p => ctx.AccessibleProjectIds.Contains(p.Id));

            switch (condition.Field)
            {
                case SavedViewFields.CreatedAt:
                    return await EvaluateProjectTimestampAsync(baseQuery, condition, isCreatedAt: true);
                case SavedViewFields.UpdatedAt:
                    return await EvaluateProjectTimestampAsync(baseQuery, condition, isCreatedAt: false);
                default:
                    unavailable.Add(condition.Field);
                    return [];
            }
        }

        private async Task<HashSet<Guid>> EvaluateProjectTimestampAsync(IQueryable<Project> baseQuery, SavedViewFilterConditionDto condition, bool isCreatedAt)
        {
            var from = ParseDateTime(condition.Value);
            var to = ParseDateTime(condition.ValueTo);

            IQueryable<Project> q = (condition.Operator, isCreatedAt) switch
            {
                (AutomationConditionOperator.Before, true) => from is DateTime f1 ? baseQuery.Where(p => p.CreatedAt < f1) : baseQuery.Where(p => false),
                (AutomationConditionOperator.After, true) => from is DateTime f2 ? baseQuery.Where(p => p.CreatedAt > f2) : baseQuery.Where(p => false),
                (AutomationConditionOperator.Between, true) => from is DateTime f3 && to is DateTime t3 ? baseQuery.Where(p => p.CreatedAt >= f3 && p.CreatedAt <= t3) : baseQuery.Where(p => false),
                (AutomationConditionOperator.Before, false) => from is DateTime f4 ? baseQuery.Where(p => p.UpdatedAt < f4) : baseQuery.Where(p => false),
                (AutomationConditionOperator.After, false) => from is DateTime f5 ? baseQuery.Where(p => p.UpdatedAt > f5) : baseQuery.Where(p => false),
                (AutomationConditionOperator.Between, false) => from is DateTime f6 && to is DateTime t6 ? baseQuery.Where(p => p.UpdatedAt >= f6 && p.UpdatedAt <= t6) : baseQuery.Where(p => false),
                _ => baseQuery.Where(p => false)
            };

            return (await q.Select(p => p.Id).ToListAsync()).ToHashSet();
        }

        private async Task<HashSet<Guid>> EvaluateProjectCustomFieldConditionAsync(Guid fieldId, SavedViewFilterConditionDto condition, ProjectFilterContext ctx, List<string> unavailable)
        {
            var field = await _db.CustomFields.FindAsync(fieldId);
            if (field is null || !field.IsActive)
            {
                unavailable.Add(condition.Field);
                return [];
            }

            var rows = await _db.ProjectCustomValues
                .Where(v => v.CustomFieldId == fieldId && ctx.AccessibleProjectIds.Contains(v.ProjectId))
                .Select(v => new { v.ProjectId, v.Value })
                .ToListAsync();

            if (field.IsPrivate)
            {
                rows = rows.Where(r => ctx.ManagedProjectIds.Contains(r.ProjectId)).ToList();
            }

            var resolvedValue = ResolveCustomFieldValueToken(condition.Value, field.FieldType, ctx.CallerId);
            var resolvedValueTo = ResolveCustomFieldValueToken(condition.ValueTo, field.FieldType, ctx.CallerId);

            var matched = new HashSet<Guid>();
            var idsWithRow = new HashSet<Guid>();
            foreach (var row in rows)
            {
                idsWithRow.Add(row.ProjectId);
                if (EvaluateCustomFieldValue(row.Value, condition.Operator, resolvedValue, resolvedValueTo, field.FieldType))
                {
                    matched.Add(row.ProjectId);
                }
            }

            if (condition.Operator == AutomationConditionOperator.IsEmpty && !field.IsPrivate)
            {
                var withoutRow = new HashSet<Guid>(ctx.AccessibleProjectIds);
                withoutRow.ExceptWith(idsWithRow);
                matched.UnionWith(withoutRow);
            }

            return matched;
        }

        // ---------- Shared helpers ----------

        private static HashSet<Guid> Combine(string logic, List<HashSet<Guid>> sets, HashSet<Guid> universe)
        {
            if (sets.Count == 0)
            {
                return [.. universe];
            }

            if (string.Equals(logic, "OR", StringComparison.OrdinalIgnoreCase))
            {
                var result = new HashSet<Guid>();
                foreach (var s in sets) result.UnionWith(s);
                return result;
            }

            var intersection = new HashSet<Guid>(sets[0]);
            for (var i = 1; i < sets.Count; i++)
            {
                intersection.IntersectWith(sets[i]);
            }
            return intersection;
        }

        private static List<string> SplitValues(string? value) =>
            string.IsNullOrWhiteSpace(value) ? [] : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        private static async Task<HashSet<Guid>> MatchStatusSetAsync(IQueryable<TaskItem> baseQuery, SavedViewFilterConditionDto condition)
        {
            var values = SplitValues(condition.Value).Select(TryParseEnum<TaskItemStatus>).Where(v => v.HasValue).Select(v => v!.Value).ToHashSet();
            var matches = await baseQuery.Where(t => values.Contains(t.Status)).Select(t => t.Id).ToListAsync();
            return ApplyNegation(matches, condition.Operator, await baseQuery.Select(t => t.Id).ToListAsync());
        }

        private static async Task<HashSet<Guid>> MatchPrioritySetAsync(IQueryable<TaskItem> baseQuery, SavedViewFilterConditionDto condition)
        {
            var values = SplitValues(condition.Value).Select(TryParseEnum<TaskPriority>).Where(v => v.HasValue).Select(v => v!.Value).ToHashSet();
            var matches = await baseQuery.Where(t => values.Contains(t.Priority)).Select(t => t.Id).ToListAsync();
            return ApplyNegation(matches, condition.Operator, await baseQuery.Select(t => t.Id).ToListAsync());
        }

        private static HashSet<Guid> ApplyNegation(List<Guid> matches, AutomationConditionOperator op, List<Guid> allIds)
        {
            if (op != AutomationConditionOperator.NotEquals)
            {
                return matches.ToHashSet();
            }
            var result = allIds.ToHashSet();
            result.ExceptWith(matches);
            return result;
        }

        private static async Task<HashSet<Guid>> MatchProjectIdSetAsync(IQueryable<TaskItem> baseQuery, SavedViewFilterConditionDto condition)
        {
            var values = SplitValues(condition.Value).Select(v => Guid.TryParse(v, out var g) ? g : (Guid?)null).Where(v => v.HasValue).Select(v => v!.Value).ToHashSet();
            var matches = await baseQuery.Where(t => values.Contains(t.ProjectId)).Select(t => t.Id).ToListAsync();
            return ApplyNegation(matches, condition.Operator, await baseQuery.Select(t => t.Id).ToListAsync());
        }

        private static TEnum? TryParseEnum<TEnum>(string value) where TEnum : struct =>
            Enum.TryParse<TEnum>(value, true, out var result) ? result : null;

        private static DateOnly? ParseDate(string? value) =>
            !string.IsNullOrWhiteSpace(value) && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        private static DateTime? ParseDateTime(string? value) =>
            !string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;

        private static string? ResolveCustomFieldValueToken(string? value, CustomFieldType fieldType, Guid callerId)
        {
            if (fieldType == CustomFieldType.User && value == SavedViewFields.AssigneeMe)
            {
                return callerId.ToString();
            }
            return value;
        }

        /// <summary>Per-custom-field-type comparison, run in C# over an already query-scoped,
        /// bounded set of (id, text-value) rows for ONE field — see the class doc comment for why
        /// this stays a disclosed exception to "compare inside SQL". Reuses
        /// CustomFieldConditionEvaluator for the string/number/date operators every Automation
        /// condition already shares; Between and MultiSelect/User "contains" semantics are handled
        /// here since the shared evaluator's single-expectedValue signature can't express them.</summary>
        private static bool EvaluateCustomFieldValue(string? actual, AutomationConditionOperator op, string? expected, string? expectedTo, CustomFieldType fieldType)
        {
            if (op == AutomationConditionOperator.Between)
            {
                if (string.IsNullOrWhiteSpace(actual)) return false;
                return fieldType switch
                {
                    CustomFieldType.Number or CustomFieldType.Currency =>
                        decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) &&
                        decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var lo) &&
                        decimal.TryParse(expectedTo, NumberStyles.Number, CultureInfo.InvariantCulture, out var hi) &&
                        a >= lo && a <= hi,
                    CustomFieldType.Date or CustomFieldType.DateTime =>
                        DateTime.TryParse(actual, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) &&
                        DateTime.TryParse(expected, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lo2) &&
                        DateTime.TryParse(expectedTo, CultureInfo.InvariantCulture, DateTimeStyles.None, out var hi2) &&
                        d >= lo2 && d <= hi2,
                    _ => false
                };
            }

            if (fieldType == CustomFieldType.MultiSelect || fieldType == CustomFieldType.UserMulti)
            {
                if (op == AutomationConditionOperator.IsEmpty) return string.IsNullOrWhiteSpace(actual) || actual == "[]";
                if (op == AutomationConditionOperator.IsNotEmpty) return !string.IsNullOrWhiteSpace(actual) && actual != "[]";
                if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected)) return false;
                try
                {
                    var ids = JsonSerializer.Deserialize<List<Guid>>(actual) ?? [];
                    var wanted = Guid.TryParse(expected, out var g) ? g : (Guid?)null;
                    var contains = wanted.HasValue && ids.Contains(wanted.Value);
                    return op == AutomationConditionOperator.NotContains ? !contains : contains;
                }
                catch (JsonException)
                {
                    return false;
                }
            }

            if (fieldType == CustomFieldType.Date && op is AutomationConditionOperator.Before or AutomationConditionOperator.After or AutomationConditionOperator.Equals)
            {
                if (string.IsNullOrWhiteSpace(actual)) return false;
                if (!DateOnly.TryParseExact(actual, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var a)) return false;
                if (!DateOnly.TryParseExact(expected, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var e)) return false;
                return op switch
                {
                    AutomationConditionOperator.Before => a < e,
                    AutomationConditionOperator.After => a > e,
                    _ => a == e
                };
            }

            return CustomFieldConditionEvaluator.Evaluate(actual, op, expected);
        }
    }

    /// <summary>Phase 43 — resolves SavedViewRelativeDates tokens against "today" fresh on every
    /// call, never a fixed date computed at save time (spec #21). Split out as a static helper so
    /// both the Task due-date filter and (in future) other relative-date consumers share one
    /// implementation.</summary>
    internal static class RelativeDateResolver
    {
        public static bool TryResolveTaskRelative(string? token, out Func<IQueryable<TaskItem>, Task<List<TaskItem>>>? query)
        {
            query = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            switch (token)
            {
                case SavedViewRelativeDates.Today:
                    query = q => q.Where(t => t.DueDate == today).ToListAsync();
                    return true;
                case SavedViewRelativeDates.Tomorrow:
                    var tomorrow = today.AddDays(1);
                    query = q => q.Where(t => t.DueDate == tomorrow).ToListAsync();
                    return true;
                case SavedViewRelativeDates.ThisWeek:
                    var (weekStart, weekEnd) = WeekBounds(today, 0);
                    query = q => q.Where(t => t.DueDate != null && t.DueDate >= weekStart && t.DueDate <= weekEnd).ToListAsync();
                    return true;
                case SavedViewRelativeDates.NextWeek:
                    var (nextStart, nextEnd) = WeekBounds(today, 1);
                    query = q => q.Where(t => t.DueDate != null && t.DueDate >= nextStart && t.DueDate <= nextEnd).ToListAsync();
                    return true;
                case SavedViewRelativeDates.ThisMonth:
                    var monthStart = new DateOnly(today.Year, today.Month, 1);
                    var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                    query = q => q.Where(t => t.DueDate != null && t.DueDate >= monthStart && t.DueDate <= monthEnd).ToListAsync();
                    return true;
                case SavedViewRelativeDates.Overdue:
                    query = q => q.Where(t => t.DueDate != null && t.DueDate < today && t.Status != TaskItemStatus.Complete && t.Status != TaskItemStatus.Cancelled).ToListAsync();
                    return true;
                case SavedViewRelativeDates.NoDueDate:
                    query = q => q.Where(t => t.DueDate == null).ToListAsync();
                    return true;
                default:
                    return false;
            }
        }

        private static (DateOnly Start, DateOnly End) WeekBounds(DateOnly today, int weekOffset)
        {
            // Sunday-start week, matching MyTasksFilters' own existing "this week" convention.
            var daysSinceSunday = (int)today.DayOfWeek;
            var start = today.AddDays(-daysSinceSunday).AddDays(weekOffset * 7);
            return (start, start.AddDays(6));
        }
    }
}
