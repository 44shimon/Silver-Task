using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    /// <summary>Phase 41 — the single source of truth for "is this raw string a valid value for
    /// this CustomField, and what's its normalized on-disk form." Extracted from TaskService's
    /// own private ValidateAndNormalizeCustomValueAsync (which only ever validated Task-scoped
    /// values) so ProjectService.SetCustomValueAsync doesn't need a second, drifting copy of the
    /// same ~15-case switch — see the spec's own repeated "do not duplicate an existing system"
    /// instruction. Both TaskService and ProjectService call this; neither owns the logic anymore.</summary>
    public interface ICustomFieldValueValidator
    {
        /// <param name="scopeProjectId">The project the entity being edited belongs to (a task's
        /// own ProjectId, or a project's own Id) — used for User/UserMulti membership checks.</param>
        Task<string?> ValidateAndNormalizeAsync(CustomField field, string? value, Guid scopeProjectId, Guid callerId, UserRole callerRole);

        /// <summary>Phase 41 — backend enforcement for "conditionally required" fields (spec #49).
        /// Called right before persisting a change to <paramref name="controllingField"/>: finds
        /// every OTHER field in the same scope whose visibility condition targets this field,
        /// and — if that condition would now evaluate true and the dependent field is required —
        /// rejects the change unless the dependent field already has a value. This is
        /// order-dependent (the dependent field must be filled in before the condition that
        /// reveals it is triggered) rather than a full atomic multi-field form submission, since
        /// this app has no such endpoint for either tasks or projects — a disclosed, deliberate
        /// scope simplification (see the Phase 41 final report).</summary>
        Task EnsureConditionalRequirementsAsync(
            CustomField controllingField,
            string? newControllingValue,
            CustomFieldEntityType entityType,
            Guid? scopeProjectId,
            IReadOnlyDictionary<Guid, string?> currentValuesByFieldId);
    }

    public class CustomFieldValueValidator(AppDbContext db, IProjectAccessService projectAccess) : ICustomFieldValueValidator
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;

        public async Task<string?> ValidateAndNormalizeAsync(CustomField field, string? value, Guid scopeProjectId, Guid callerId, UserRole callerRole)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (field.IsRequired)
                {
                    throw new ValidationException($"'{field.Name}' is required and cannot be cleared.");
                }
                return null;
            }

            // A deactivated field keeps its existing values readable (never force-cleared) but
            // can't be given a new value.
            if (!field.IsActive)
            {
                throw new ValidationException($"'{field.Name}' has been disabled and can no longer be set.");
            }

            switch (field.FieldType)
            {
                case CustomFieldType.Text:
                case CustomFieldType.LongText:
                    return EnforceMaxLength(field, value.Trim());

                case CustomFieldType.Number:
                case CustomFieldType.Currency:
                    return ValidateNumber(field, value);

                case CustomFieldType.Date:
                    if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        throw new ValidationException($"'{value}' is not a valid date (expected YYYY-MM-DD) for field '{field.Name}'.");
                    }
                    return value.Trim();

                case CustomFieldType.DateTime:
                    if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsedDateTime))
                    {
                        throw new ValidationException($"'{value}' is not a valid date/time for field '{field.Name}'.");
                    }
                    return parsedDateTime.ToString("O", CultureInfo.InvariantCulture);

                case CustomFieldType.Checkbox:
                    if (value is not ("true" or "false"))
                    {
                        throw new ValidationException($"Checkbox field '{field.Name}' must be 'true' or 'false'.");
                    }
                    return value;

                case CustomFieldType.Dropdown:
                    if (!field.Options.Any(o => o.Id.ToString() == value))
                    {
                        throw new ValidationException($"'{value}' is not a valid option for field '{field.Name}'.");
                    }
                    return value;

                case CustomFieldType.MultiSelect:
                    {
                        var optionIds = ParseGuidArray(value, field.Name);
                        var validIds = field.Options.Select(o => o.Id).ToHashSet();
                        if (optionIds.Any(id => !validIds.Contains(id)))
                        {
                            throw new ValidationException($"One or more selected options are not valid for field '{field.Name}'.");
                        }
                        return JsonSerializer.Serialize(optionIds);
                    }

                case CustomFieldType.User:
                    if (!Guid.TryParse(value, out var userId) || !await _projectAccess.IsMemberAsync(scopeProjectId, userId))
                    {
                        throw new ValidationException($"'{value}' is not a valid project member for field '{field.Name}'.");
                    }
                    return value;

                case CustomFieldType.UserMulti:
                    {
                        var userIds = ParseGuidArray(value, field.Name);
                        foreach (var id in userIds)
                        {
                            if (!await _projectAccess.IsMemberAsync(scopeProjectId, id))
                            {
                                throw new ValidationException($"One or more selected users are not valid project members for field '{field.Name}'.");
                            }
                        }
                        return JsonSerializer.Serialize(userIds);
                    }

                case CustomFieldType.TaskReference:
                    return await ValidateTaskReferenceAsync(field, value, callerId, callerRole);

                case CustomFieldType.ProjectReference:
                    return await ValidateProjectReferenceAsync(field, value, callerId, callerRole);

                case CustomFieldType.Link:
                    return NormalizeLinkValue(value, field.Name);

                case CustomFieldType.Url:
                    return NormalizeUrlValue(value, field.Name);

                case CustomFieldType.Email:
                    return ValidateEmail(field, value);

                case CustomFieldType.Phone:
                    return ValidatePhone(field, value);

                default:
                    throw new ValidationException($"Unsupported field type for '{field.Name}'.");
            }
        }

        public async Task EnsureConditionalRequirementsAsync(
            CustomField controllingField,
            string? newControllingValue,
            CustomFieldEntityType entityType,
            Guid? scopeProjectId,
            IReadOnlyDictionary<Guid, string?> currentValuesByFieldId)
        {
            var dependents = await _db.CustomFields
                .Where(f => f.ConditionFieldId == controllingField.Id && f.EntityType == entityType && f.IsRequired)
                .Where(f => scopeProjectId == null || f.ProjectId == null || f.ProjectId == scopeProjectId)
                .ToListAsync();

            foreach (var dependent in dependents)
            {
                if (dependent.ConditionOperator is not AutomationConditionOperator op)
                {
                    continue;
                }

                var conditionNowTrue = CustomFieldConditionEvaluator.Evaluate(newControllingValue, op, dependent.ConditionValue);
                if (!conditionNowTrue)
                {
                    continue;
                }

                var dependentHasValue = currentValuesByFieldId.TryGetValue(dependent.Id, out var existingValue) && !string.IsNullOrWhiteSpace(existingValue);
                if (!dependentHasValue)
                {
                    throw new ValidationException(
                        $"'{dependent.Name}' is required when '{controllingField.Name}' {DescribeOperator(op)} '{dependent.ConditionValue}'.");
                }
            }
        }

        private static string DescribeOperator(AutomationConditionOperator op) => op switch
        {
            AutomationConditionOperator.Equals => "equals",
            AutomationConditionOperator.NotEquals => "does not equal",
            AutomationConditionOperator.Contains => "contains",
            AutomationConditionOperator.NotContains => "does not contain",
            AutomationConditionOperator.GreaterThan => "is greater than",
            AutomationConditionOperator.LessThan => "is less than",
            AutomationConditionOperator.GreaterThanOrEqual => "is at least",
            AutomationConditionOperator.LessThanOrEqual => "is at most",
            AutomationConditionOperator.Before => "is before",
            AutomationConditionOperator.After => "is after",
            _ => "matches"
        };

        private static string EnforceMaxLength(CustomField field, string value)
        {
            if (field.MaxLength is int maxLength && value.Length > maxLength)
            {
                throw new ValidationException($"'{field.Name}' cannot be longer than {maxLength} characters.");
            }
            return value;
        }

        private static string ValidateNumber(CustomField field, string value)
        {
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new ValidationException($"'{value}' is not a valid number for field '{field.Name}'.");
            }

            if (field.MinValue is decimal min && parsed < min)
            {
                throw new ValidationException($"'{field.Name}' must be at least {min}.");
            }
            if (field.MaxValue is decimal max && parsed > max)
            {
                throw new ValidationException($"'{field.Name}' must be at most {max}.");
            }

            if (field.DecimalPlaces is int places)
            {
                var rounded = Math.Round(parsed, places, MidpointRounding.AwayFromZero);
                if (rounded != parsed)
                {
                    throw new ValidationException(
                        places == 0
                            ? $"'{field.Name}' must be a whole number."
                            : $"'{field.Name}' can have at most {places} decimal place{(places == 1 ? "" : "s")}.");
                }
            }

            return value.Trim();
        }

        private async Task<string> ValidateTaskReferenceAsync(CustomField field, string value, Guid callerId, UserRole callerRole)
        {
            if (!Guid.TryParse(value, out var taskId))
            {
                throw new ValidationException($"'{value}' is not a valid task reference for field '{field.Name}'.");
            }

            var referenced = await _db.Tasks
                .Where(t => t.Id == taskId)
                .Select(t => new { t.ProjectId, OwnerId = t.Project!.OwnerId })
                .FirstOrDefaultAsync();

            if (referenced is null)
            {
                throw new ValidationException($"The referenced task for '{field.Name}' does not exist.");
            }

            // "Respect project permissions" (spec #32) — the caller must actually be able to see
            // the task they're linking to, same participate tier as viewing that task directly.
            await _projectAccess.EnsureCanParticipateAsync(referenced.ProjectId, referenced.OwnerId, callerId, callerRole);

            return value;
        }

        private async Task<string> ValidateProjectReferenceAsync(CustomField field, string value, Guid callerId, UserRole callerRole)
        {
            if (!Guid.TryParse(value, out var projectId))
            {
                throw new ValidationException($"'{value}' is not a valid project reference for field '{field.Name}'.");
            }

            var referenced = await _db.Projects
                .Where(p => p.Id == projectId)
                .Select(p => new { p.OwnerId })
                .FirstOrDefaultAsync();

            if (referenced is null)
            {
                throw new ValidationException($"The referenced project for '{field.Name}' does not exist.");
            }

            await _projectAccess.EnsureCanParticipateAsync(projectId, referenced.OwnerId, callerId, callerRole);

            return value;
        }

        private static string NormalizeUrlValue(string value, string fieldName)
        {
            var originalUrl = value.Trim();
            var url = originalUrl;

            // Users naturally type "google.com" without a scheme — treat that like a browser
            // address bar would, rather than rejecting it.
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            // Only http/https are ever accepted — javascript:/data:/etc. are rejected outright,
            // never merely hidden by the frontend (spec #29's own explicit requirement).
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrEmpty(uri.Host))
            {
                throw new ValidationException($"'{originalUrl}' is not a valid URL for field '{fieldName}'.");
            }

            return url;
        }

        private static string NormalizeLinkValue(string value, string fieldName)
        {
            LinkValue? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<LinkValue>(value);
            }
            catch (JsonException)
            {
                throw new ValidationException($"'{value}' is not a valid link value for field '{fieldName}'.");
            }

            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Url))
            {
                throw new ValidationException($"A URL is required for link field '{fieldName}'.");
            }

            var url = NormalizeUrlValue(parsed.Url, fieldName);
            return JsonSerializer.Serialize(new LinkValue { Label = parsed.Label?.Trim() ?? string.Empty, Url = url });
        }

        private static string ValidateEmail(CustomField field, string value)
        {
            var trimmed = value.Trim();
            // .NET's own MailAddress parser, not a hand-rolled regex — reuses the framework's
            // existing email-format validation rather than inventing a second one.
            if (!System.Net.Mail.MailAddress.TryCreate(trimmed, out _))
            {
                throw new ValidationException($"'{trimmed}' is not a valid email address for field '{field.Name}'.");
            }
            return EnforceMaxLength(field, trimmed);
        }

        private static readonly System.Text.RegularExpressions.Regex PhonePattern =
            new(@"^[0-9+()\-.\s]{7,25}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string ValidatePhone(CustomField field, string value)
        {
            var trimmed = value.Trim();
            // A lightweight, permissive format check (digits/spaces/+/-/./parentheses, 7-25
            // characters) — this app has no existing phone-formatting system to reuse (confirmed
            // by grep), so this is deliberately simple rather than a full international
            // libphonenumber-style validator.
            if (!PhonePattern.IsMatch(trimmed))
            {
                throw new ValidationException($"'{trimmed}' is not a valid phone number for field '{field.Name}'.");
            }
            return EnforceMaxLength(field, trimmed);
        }

        private static List<Guid> ParseGuidArray(string value, string fieldName)
        {
            try
            {
                var ids = JsonSerializer.Deserialize<List<Guid>>(value);
                return ids ?? [];
            }
            catch (JsonException)
            {
                throw new ValidationException($"'{value}' is not a valid selection for field '{fieldName}'.");
            }
        }

        private class LinkValue
        {
            [JsonPropertyName("label")]
            public string? Label { get; set; }

            [JsonPropertyName("url")]
            public string Url { get; set; } = string.Empty;
        }
    }
}
