/** Parses a DateOnly ("YYYY-MM-DD") string using local date components, avoiding the
 * off-by-one shift that `new Date(dateOnlyString)` can produce (it's parsed as UTC
 * midnight, then rendered in the local timezone). */
export function formatDate(value: string | null): string {
  if (!value) {
    return '';
  }
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day).toLocaleDateString();
}

/** Formats a full ISO timestamp (e.g. `createdAt`/`updatedAt`), unlike `formatDate` above
 * which is specifically for DateOnly ("YYYY-MM-DD") fields like Due Date. */
export function formatDateTime(value: string | null): string {
  if (!value) {
    return '';
  }
  return new Date(value).toLocaleDateString();
}

/** "Just now" / "5m ago" / "2h ago" / "Yesterday" / an absolute date once it's old enough that a
 * relative label stops being useful — used specifically for the notification center (Phase 36),
 * not a general-purpose replacement for formatDateTime above. */
export function formatRelativeTime(value: string): string {
  const date = new Date(value);
  const diffMs = Date.now() - date.getTime();
  const diffMinutes = Math.floor(diffMs / 60_000);

  if (diffMinutes < 1) return 'Just now';
  if (diffMinutes < 60) return `${diffMinutes}m ago`;

  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) return `${diffHours}h ago`;

  const startOfToday = new Date();
  startOfToday.setHours(0, 0, 0, 0);
  const startOfDate = new Date(date);
  startOfDate.setHours(0, 0, 0, 0);
  const dayDiff = Math.round((startOfToday.getTime() - startOfDate.getTime()) / 86_400_000);

  if (dayDiff === 1) return 'Yesterday';
  if (dayDiff < 7) return `${dayDiff}d ago`;

  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: date.getFullYear() === new Date().getFullYear() ? undefined : 'numeric' });
}
