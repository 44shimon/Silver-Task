/** Local-timezone "today" as a DateOnly string ("YYYY-MM-DD"), matching the format the API
 * uses for Due Date so it can be compared with simple string comparison. */
export function todayDateOnly(): string {
  return toDateOnly(new Date());
}

/** DateOnly string for `daysAhead` days from today (e.g. 6 for a rolling 7-day "this week"
 * window that starts today). */
export function daysFromTodayDateOnly(daysAhead: number): string {
  const date = new Date();
  date.setDate(date.getDate() + daysAhead);
  return toDateOnly(date);
}

function toDateOnly(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
