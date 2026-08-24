import type { Task } from '@/types/task';
import { parseDateOnly } from './calendarGrid';

export function daysBetween(a: Date, b: Date): number {
  const msPerDay = 24 * 60 * 60 * 1000;
  const utcA = Date.UTC(a.getFullYear(), a.getMonth(), a.getDate());
  const utcB = Date.UTC(b.getFullYear(), b.getMonth(), b.getDate());
  return Math.round((utcB - utcA) / msPerDay);
}

/** A task needs at least one of Start Date/Due Date to be placed as a bar. */
export function tasksWithDates(tasks: Task[]): Task[] {
  return tasks.filter((task) => task.startDate !== null || task.dueDate !== null);
}

/** Tasks with neither date — can't be positioned on the chart. Kept visible in a separate
 * "Unscheduled" tray rather than silently dropped, same principle as the Calendar view's
 * No Due Date tray. */
export function tasksWithoutDates(tasks: Task[]): Task[] {
  return tasks.filter((task) => task.startDate === null && task.dueDate === null);
}

/** The bar's displayed start/end. Falls back to whichever single date is present so a task
 * with only one of the two still renders as a one-day bar instead of being unplaceable. */
export function displayRange(task: Task): { start: Date; end: Date } {
  const start = parseDateOnly(task.startDate ?? task.dueDate!);
  const end = parseDateOnly(task.dueDate ?? task.startDate!);
  return start <= end ? { start, end } : { start: end, end: start };
}
