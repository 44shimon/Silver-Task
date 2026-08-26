using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    /// <summary>Builds the single human-readable "Every Monday" / "Every 2 weeks on Mon, Wed"
    /// string used both by the API (RecurrenceRuleDto.ScheduleDescription, so the frontend never
    /// has to re-derive this formatting itself) and by RecurringTaskService's own activity-log
    /// entries ("changed recurrence from X to Y") — one description format, not two.</summary>
    public static class RecurrenceDescriptionBuilder
    {
        public static string Describe(RecurringTask rule)
        {
            var text = DescribeInterval(rule.Frequency, rule.Interval) + DescribeAnchor(rule);

            if (rule.EndDate is DateOnly end)
            {
                text += $", until {end:MMM d, yyyy}";
            }
            else if (rule.MaxOccurrences is int max)
            {
                text += $", {max} time{(max == 1 ? "" : "s")}";
            }

            return text;
        }

        private static string DescribeInterval(RecurrenceFrequency frequency, int interval)
        {
            var unit = frequency switch
            {
                RecurrenceFrequency.Daily => "day",
                RecurrenceFrequency.Weekly => "week",
                RecurrenceFrequency.Monthly => "month",
                RecurrenceFrequency.Yearly => "year",
                _ => "occurrence"
            };
            return interval <= 1 ? $"Every {unit}" : $"Every {interval} {unit}s";
        }

        private static string DescribeAnchor(RecurringTask rule) => rule.Frequency switch
        {
            RecurrenceFrequency.Weekly when rule.DaysOfWeek != RecurrenceDayOfWeek.None =>
                $" on {DescribeDays(rule.DaysOfWeek)}",
            RecurrenceFrequency.Monthly when rule.DayOfMonth is int dom => $" on day {dom}",
            RecurrenceFrequency.Yearly when rule.MonthOfYear is int moy && rule.DayOfMonth is int day =>
                $" on {DescribeMonthDay(moy, day)}",
            _ => string.Empty
        };

        private static string DescribeMonthDay(int month, int day)
        {
            var clampedDay = Math.Min(day, DateTime.DaysInMonth(2000, month));
            return new DateOnly(2000, month, clampedDay).ToString("MMMM d");
        }

        private static string DescribeDays(RecurrenceDayOfWeek mask)
        {
            var names = new List<string>();
            void Add(RecurrenceDayOfWeek flag, string name)
            {
                if ((mask & flag) != 0)
                {
                    names.Add(name);
                }
            }

            // Calendar (Sun-first) order, matching the rest of the app's week-start convention
            // (utils/calendarGrid.ts's startOfWeek), not enum-declaration order.
            Add(RecurrenceDayOfWeek.Sunday, "Sun");
            Add(RecurrenceDayOfWeek.Monday, "Mon");
            Add(RecurrenceDayOfWeek.Tuesday, "Tue");
            Add(RecurrenceDayOfWeek.Wednesday, "Wed");
            Add(RecurrenceDayOfWeek.Thursday, "Thu");
            Add(RecurrenceDayOfWeek.Friday, "Fri");
            Add(RecurrenceDayOfWeek.Saturday, "Sat");

            return string.Join(", ", names);
        }
    }
}
