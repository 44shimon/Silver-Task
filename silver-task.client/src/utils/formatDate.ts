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
