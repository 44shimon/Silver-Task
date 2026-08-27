namespace Silver_Task.Server.Common
{
    /// <summary>Centralizes every "what is today/this week/this month for this user" calculation
    /// the dashboard needs (Phase 37) — a single place, not duplicated per controller/service
    /// method, and always resolved in the *caller's* configured UserPreference.TimeZone rather
    /// than server/UTC time (the due-soon/overdue sweep in NotificationService predates this and
    /// still uses server UTC — a known, disclosed inconsistency; this phase's new dashboard
    /// queries are the first to do it correctly).</summary>
    public static class DashboardDateHelper
    {
        public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }

        /// <summary>"Today" as a DateOnly, in the given time zone, derived from the current UTC
        /// instant — the single source of truth every other range below is built from.</summary>
        public static DateOnly TodayInZone(TimeZoneInfo timeZone) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));

        /// <summary>Monday-start week containing <paramref name="today"/> — matches this app's
        /// existing "This Week" convention elsewhere (recurring task/digest windows are calendar-
        /// day based, not ISO-week-number based, so Monday-Sunday is the simplest unambiguous
        /// choice and needs no extra configuration).</summary>
        public static (DateOnly Start, DateOnly End) WeekRange(DateOnly today)
        {
            var offset = ((int)today.DayOfWeek + 6) % 7; // Monday = 0 ... Sunday = 6
            var start = today.AddDays(-offset);
            return (start, start.AddDays(6));
        }

        public static (DateOnly Start, DateOnly End) MonthRange(DateOnly today)
        {
            var start = new DateOnly(today.Year, today.Month, 1);
            return (start, start.AddMonths(1).AddDays(-1));
        }

        public static (DateOnly Start, DateOnly End) QuarterRange(DateOnly today)
        {
            var quarterStartMonth = ((today.Month - 1) / 3 * 3) + 1;
            var start = new DateOnly(today.Year, quarterStartMonth, 1);
            return (start, start.AddMonths(3).AddDays(-1));
        }

        public static (DateOnly Start, DateOnly End) YearRange(DateOnly today)
        {
            var start = new DateOnly(today.Year, 1, 1);
            return (start, new DateOnly(today.Year, 12, 31));
        }

        /// <summary>Phase 38's report date-range filter — Today/Yesterday/This Week/Last Week/
        /// This Month/Last Month/This Quarter/This Year/Custom. Custom requires both
        /// <paramref name="customStart"/> and <paramref name="customEnd"/>; if either is missing
        /// (or the key is unrecognized), falls back to "This Month" — same "don't 500 on a stray
        /// param" leniency as UpcomingRange/StatsRange, since a malformed filter should degrade to
        /// a sensible default, not break the whole report.</summary>
        public static (DateOnly Start, DateOnly End) ReportDateRange(DateOnly today, string? key, DateOnly? customStart, DateOnly? customEnd)
        {
            switch (key)
            {
                case "today":
                    return (today, today);
                case "yesterday":
                    return (today.AddDays(-1), today.AddDays(-1));
                case "thisWeek":
                    return WeekRange(today);
                case "lastWeek":
                    var (thisWeekStart, _) = WeekRange(today);
                    return (thisWeekStart.AddDays(-7), thisWeekStart.AddDays(-1));
                case "thisMonth":
                    return MonthRange(today);
                case "lastMonth":
                    return MonthRange(today.AddMonths(-1) is var d && d.Day > DateTime.DaysInMonth(d.Year, d.Month) ? new DateOnly(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month)) : d);
                case "thisQuarter":
                    return QuarterRange(today);
                case "thisYear":
                    return YearRange(today);
                case "custom" when customStart is DateOnly cs && customEnd is DateOnly ce && cs <= ce:
                    return (cs, ce);
                default:
                    return MonthRange(today);
            }
        }

        /// <summary>Resolves the dashboard's "Upcoming" widget range query param
        /// ("today"/"tomorrow"/"7days"/"30days") to a concrete [start, end] window (inclusive).
        /// Unrecognized values fall back to the documented default (7 days) rather than erroring —
        /// same "don't 500 on a stray query param" leniency the rest of this app's optional
        /// filters already have.</summary>
        public static (DateOnly Start, DateOnly End) UpcomingRange(DateOnly today, string? range) => range switch
        {
            "today" => (today, today),
            "tomorrow" => (today.AddDays(1), today.AddDays(1)),
            "30days" => (today, today.AddDays(29)),
            _ => (today, today.AddDays(6)),
        };

        /// <summary>Resolves the dashboard's statistics date-range param ("today"/"week"/"month")
        /// — default "week" per the spec's own stated default.</summary>
        public static (DateOnly Start, DateOnly End) StatsRange(DateOnly today, string? range) => range switch
        {
            "today" => (today, today),
            "month" => MonthRange(today),
            _ => WeekRange(today),
        };

        /// <summary>The real UTC instant that midnight on <paramref name="date"/> corresponds to
        /// *in the given time zone* — e.g. for America/New_York, "start of day" is 04:00 or 05:00
        /// UTC, not 00:00 UTC. Needed anywhere a DateOnly boundary (computed in the user's zone)
        /// has to be compared against a `timestamp with time zone` column (which Npgsql only
        /// accepts as Kind=Utc — naively relabeling a local midnight as UTC via
        /// DateTime.SpecifyKind would compile and run, but silently compare against the wrong
        /// instant for every non-UTC user).</summary>
        public static DateTime StartOfDayUtc(DateOnly date, TimeZoneInfo timeZone)
        {
            var unspecified = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
        }
    }
}
