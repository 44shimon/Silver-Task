using System.Globalization;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    /// <summary>Phase 41 — evaluates a single CustomField.ConditionOperator against a controlling
    /// field's raw stored value and the condition's own ConditionValue. Reuses
    /// AutomationConditionOperator (Phase 35) rather than a second operator enum — see the spec's
    /// own repeated "reuse existing" instruction and CustomField.ConditionOperator's own doc
    /// comment. Numeric/date comparisons fall back to ordinal string comparison when either side
    /// doesn't parse, so this never throws on a malformed value — it just evaluates false.</summary>
    public static class CustomFieldConditionEvaluator
    {
        public static bool Evaluate(string? actualValue, AutomationConditionOperator op, string? expectedValue)
        {
            switch (op)
            {
                case AutomationConditionOperator.IsEmpty:
                    return string.IsNullOrWhiteSpace(actualValue);
                case AutomationConditionOperator.IsNotEmpty:
                    return !string.IsNullOrWhiteSpace(actualValue);
            }

            if (actualValue is null)
            {
                // Every remaining operator needs an actual value to compare against.
                return false;
            }

            switch (op)
            {
                case AutomationConditionOperator.Equals:
                    return string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase);
                case AutomationConditionOperator.NotEquals:
                    return !string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase);
                case AutomationConditionOperator.Contains:
                    return expectedValue is not null && actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase);
                case AutomationConditionOperator.NotContains:
                    return expectedValue is null || !actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase);
                case AutomationConditionOperator.StartsWith:
                    return expectedValue is not null && actualValue.StartsWith(expectedValue, StringComparison.OrdinalIgnoreCase);
                case AutomationConditionOperator.EndsWith:
                    return expectedValue is not null && actualValue.EndsWith(expectedValue, StringComparison.OrdinalIgnoreCase);
                case AutomationConditionOperator.GreaterThan:
                case AutomationConditionOperator.After:
                    return Compare(actualValue, expectedValue) > 0;
                case AutomationConditionOperator.LessThan:
                case AutomationConditionOperator.Before:
                    return Compare(actualValue, expectedValue) < 0;
                case AutomationConditionOperator.GreaterThanOrEqual:
                    return Compare(actualValue, expectedValue) >= 0;
                case AutomationConditionOperator.LessThanOrEqual:
                    return Compare(actualValue, expectedValue) <= 0;
                default:
                    return false;
            }
        }

        private static int Compare(string actual, string? expected)
        {
            if (expected is null)
            {
                return 0;
            }

            if (decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var actualNumber) &&
                decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var expectedNumber))
            {
                return actualNumber.CompareTo(expectedNumber);
            }

            if (DateTime.TryParse(actual, CultureInfo.InvariantCulture, DateTimeStyles.None, out var actualDate) &&
                DateTime.TryParse(expected, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expectedDate))
            {
                return actualDate.CompareTo(expectedDate);
            }

            return string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
