using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    /// <summary>
    /// Pure, stateless recurrence date math — no DB access, no side effects, so the same rule and
    /// "after" date always produce the same next occurrence date no matter when/how often this is
    /// called. Kept deliberately separate from RecurringTaskService so the scheduling logic itself
    /// can be reasoned about (and, if a test project is ever added, tested) in isolation from
    /// persistence/permissions/notifications.
    /// </summary>
    public static class RecurrenceCalculator
    {
        // Bounds every search loop below — with Interval capped at 365/52/24/10 (see
        // RecurringTaskService.ValidateRule) and a bounded generation window, a real call never
        // gets close to this; it exists purely so a pathological/corrupted rule can't spin forever.
        private const int MaxSearchIterations = 2000;

        /// <summary>The next occurrence date strictly after <paramref name="afterDate"/> matching
        /// <paramref name="rule"/>'s pattern. Does not consider EndDate/MaxOccurrences — the
        /// caller checks those against the returned date/running count separately.</summary>
        public static DateOnly? ComputeNextOccurrence(RecurringTask rule, DateOnly afterDate) =>
            rule.Frequency switch
            {
                RecurrenceFrequency.Daily => afterDate.AddDays(Math.Max(1, rule.Interval)),
                RecurrenceFrequency.Weekly => NextWeekly(rule, afterDate),
                RecurrenceFrequency.Monthly => NextMonthly(rule, afterDate),
                RecurrenceFrequency.Yearly => NextYearly(rule, afterDate),
                _ => null
            };

        /// <summary>Walks forward day by day (bounded) looking for a day whose weekday is in the
        /// selected set *and* whose week is aligned to the interval relative to the rule's own
        /// start week — e.g. "every 2 weeks on Mon/Wed" fires both days in an active week, then
        /// skips the next Interval-1 weeks entirely, rather than treating each weekday independently.</summary>
        private static DateOnly NextWeekly(RecurringTask rule, DateOnly afterDate)
        {
            var interval = Math.Max(1, rule.Interval);
            var mask = rule.DaysOfWeek == RecurrenceDayOfWeek.None
                ? DayOfWeekToFlag(rule.StartDate.DayOfWeek)
                : rule.DaysOfWeek;

            var baseWeekStart = WeekStart(rule.StartDate);
            var candidate = afterDate.AddDays(1);

            for (var i = 0; i < MaxSearchIterations; i++)
            {
                var weeksSinceBase = (WeekStart(candidate).DayNumber - baseWeekStart.DayNumber) / 7;
                var alignedWeek = weeksSinceBase >= 0 && weeksSinceBase % interval == 0;

                if (alignedWeek && (mask & DayOfWeekToFlag(candidate.DayOfWeek)) != 0)
                {
                    return candidate;
                }

                candidate = candidate.AddDays(1);
            }

            return afterDate.AddDays(interval * 7);
        }

        /// <summary>Steps forward by whole Interval-month jumps (aligned to the rule's own start
        /// month) until landing on a clamped date after afterDate — "Jan 31 every month" yields
        /// Feb 28, Mar 31, Apr 30, ... never an invalid Feb 31.</summary>
        private static DateOnly NextMonthly(RecurringTask rule, DateOnly afterDate)
        {
            var interval = Math.Max(1, rule.Interval);
            var day = rule.DayOfMonth is >= 1 and <= 31 ? rule.DayOfMonth.Value : rule.StartDate.Day;

            var startMonthIndex = rule.StartDate.Year * 12 + (rule.StartDate.Month - 1);
            var afterMonthIndex = afterDate.Year * 12 + (afterDate.Month - 1);
            var stepsSoFar = (afterMonthIndex - startMonthIndex) / interval;
            var candidateMonthIndex = startMonthIndex + stepsSoFar * interval;

            for (var i = 0; i < MaxSearchIterations; i++)
            {
                var candidate = MakeClampedDate(candidateMonthIndex, day);
                if (candidate > afterDate)
                {
                    return candidate;
                }
                candidateMonthIndex += interval;
            }

            return afterDate.AddMonths(interval);
        }

        /// <summary>Same shape as NextMonthly but stepping whole years — handles Feb 29 on a
        /// non-leap year by clamping to Feb 28, never crashing.</summary>
        private static DateOnly NextYearly(RecurringTask rule, DateOnly afterDate)
        {
            var interval = Math.Max(1, rule.Interval);
            var month = rule.MonthOfYear is >= 1 and <= 12 ? rule.MonthOfYear.Value : rule.StartDate.Month;
            var day = rule.DayOfMonth is >= 1 and <= 31 ? rule.DayOfMonth.Value : rule.StartDate.Day;

            var stepsSoFar = (afterDate.Year - rule.StartDate.Year) / interval;
            var candidateYear = rule.StartDate.Year + stepsSoFar * interval;

            for (var i = 0; i < MaxSearchIterations; i++)
            {
                var candidate = MakeClampedDate(candidateYear, month, day);
                if (candidate > afterDate)
                {
                    return candidate;
                }
                candidateYear += interval;
            }

            return afterDate.AddYears(interval);
        }

        private static DateOnly MakeClampedDate(int monthIndex, int day)
        {
            var year = monthIndex / 12;
            var month = monthIndex % 12 + 1;
            return MakeClampedDate(year, month, day);
        }

        private static DateOnly MakeClampedDate(int year, int month, int day) =>
            new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

        private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(int)date.DayOfWeek);

        private static RecurrenceDayOfWeek DayOfWeekToFlag(DayOfWeek day) => day switch
        {
            DayOfWeek.Sunday => RecurrenceDayOfWeek.Sunday,
            DayOfWeek.Monday => RecurrenceDayOfWeek.Monday,
            DayOfWeek.Tuesday => RecurrenceDayOfWeek.Tuesday,
            DayOfWeek.Wednesday => RecurrenceDayOfWeek.Wednesday,
            DayOfWeek.Thursday => RecurrenceDayOfWeek.Thursday,
            DayOfWeek.Friday => RecurrenceDayOfWeek.Friday,
            DayOfWeek.Saturday => RecurrenceDayOfWeek.Saturday,
            _ => RecurrenceDayOfWeek.None
        };

        public static RecurrenceDayOfWeek ToMask(IEnumerable<DayOfWeek>? days)
        {
            var mask = RecurrenceDayOfWeek.None;
            if (days is null)
            {
                return mask;
            }
            foreach (var day in days)
            {
                mask |= DayOfWeekToFlag(day);
            }
            return mask;
        }

        public static List<DayOfWeek> FromMask(RecurrenceDayOfWeek mask)
        {
            var days = new List<DayOfWeek>();
            void Add(RecurrenceDayOfWeek flag, DayOfWeek day) { if ((mask & flag) != 0) days.Add(day); }
            Add(RecurrenceDayOfWeek.Sunday, DayOfWeek.Sunday);
            Add(RecurrenceDayOfWeek.Monday, DayOfWeek.Monday);
            Add(RecurrenceDayOfWeek.Tuesday, DayOfWeek.Tuesday);
            Add(RecurrenceDayOfWeek.Wednesday, DayOfWeek.Wednesday);
            Add(RecurrenceDayOfWeek.Thursday, DayOfWeek.Thursday);
            Add(RecurrenceDayOfWeek.Friday, DayOfWeek.Friday);
            Add(RecurrenceDayOfWeek.Saturday, DayOfWeek.Saturday);
            return days;
        }
    }
}
