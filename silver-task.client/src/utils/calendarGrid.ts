import type { Task } from '@/types/task';

/** Parses a DateOnly ("YYYY-MM-DD") string as local date components — same reasoning as
 * formatDate: avoids the off-by-one shift `new Date(dateOnlyString)` can produce by parsing
 * it as UTC midnight and then rendering in the local timezone. */
export function parseDateOnly(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

export function toDateOnly(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function addDays(date: Date, days: number): Date {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}

export function addMonths(date: Date, months: number): Date {
  const next = new Date(date);
  next.setMonth(next.getMonth() + months);
  return next;
}

export function isSameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

function startOfWeek(date: Date): Date {
  const start = new Date(date);
  start.setDate(start.getDate() - start.getDay());
  return start;
}

/** The 7 dates (Sunday–Saturday) containing `anchor`. */
export function buildWeekDays(anchor: Date): Date[] {
  const start = startOfWeek(anchor);
  return Array.from({ length: 7 }, (_, i) => addDays(start, i));
}

/** Every date in the calendar-grid weeks that overlap `anchor`'s month — always a whole
 * number of 7-day rows (so the grid stays a clean rectangle), including the leading/trailing
 * days from adjacent months that fill out the first/last week. */
export function buildMonthDays(anchor: Date): Date[] {
  const firstOfMonth = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
  const lastOfMonth = new Date(anchor.getFullYear(), anchor.getMonth() + 1, 0);
  const start = startOfWeek(firstOfMonth);
  const end = startOfWeek(lastOfMonth);
  end.setDate(end.getDate() + 6);

  const days: Date[] = [];
  for (let day = start; day <= end; day = addDays(day, 1)) {
    days.push(day);
  }
  return days;
}

/** Tasks with a due date, keyed by that DateOnly string. Tasks without one are deliberately
 * excluded — see tasksWithoutDueDate, rendered in a separate always-visible tray so they don't
 * just disappear from the view. */
export function groupTasksByDueDate(tasks: Task[]): Map<string, Task[]> {
  const map = new Map<string, Task[]>();
  for (const task of tasks) {
    if (!task.dueDate) {
      continue;
    }
    const existing = map.get(task.dueDate);
    if (existing) {
      existing.push(task);
    } else {
      map.set(task.dueDate, [task]);
    }
  }
  return map;
}

export function tasksWithoutDueDate(tasks: Task[]): Task[] {
  return tasks.filter((task) => task.dueDate === null);
}
