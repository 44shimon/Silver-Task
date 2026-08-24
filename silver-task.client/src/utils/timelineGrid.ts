import type { Task } from '@/types/task';
import { addDays, parseDateOnly, startOfWeek, toDateOnly } from './calendarGrid';

export type TimelineScale = 'day' | 'week' | 'month';

/** Shared by the Timeline and Gantt views (Gantt is Timeline's chart engine plus a
 * project/task grouping layer — not a second implementation of zoom/ruler/bar math). */
export const PIXELS_PER_DAY: Record<TimelineScale, number> = { day: 36, week: 12, month: 4 };

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

/** The chart's overall date window — every scheduled task's range, padded a few days on each
 * side, so nothing needs scrolling to be found by default. Falls back to a window around today
 * when nothing is scheduled yet. */
export function computeDateRange(scheduled: Task[]): { rangeStart: Date; rangeEnd: Date } {
  if (scheduled.length === 0) {
    const today = new Date();
    return { rangeStart: addDays(today, -7), rangeEnd: addDays(today, 21) };
  }

  let minDate: Date | null = null;
  let maxDate: Date | null = null;
  for (const task of scheduled) {
    const { start, end } = displayRange(task);
    if (!minDate || start < minDate) {
      minDate = start;
    }
    if (!maxDate || end > maxDate) {
      maxDate = end;
    }
  }

  return { rangeStart: addDays(minDate!, -3), rangeEnd: addDays(maxDate!, 3) };
}

export interface TimelineTick {
  key: string;
  left: number;
  label: string;
}

export function buildTimelineTicks(scale: TimelineScale, rangeStart: Date, rangeEnd: Date): TimelineTick[] {
  if (scale === 'day') {
    const totalDays = daysBetween(rangeStart, rangeEnd) + 1;
    return Array.from({ length: totalDays }, (_, i) => {
      const date = addDays(rangeStart, i);
      return {
        key: toDateOnly(date),
        left: i * PIXELS_PER_DAY.day,
        label: String(date.getDate()),
      };
    });
  }

  if (scale === 'week') {
    const ticks: TimelineTick[] = [];
    for (let date = startOfWeek(rangeStart); date <= rangeEnd; date = addDays(date, 7)) {
      const left = daysBetween(rangeStart, date) * PIXELS_PER_DAY.week;
      // startOfWeek(rangeStart) can land a few days before rangeStart itself — skip the
      // partial tick rather than rendering it at a negative (invisible/clipped) position.
      if (left < 0) {
        continue;
      }
      ticks.push({
        key: toDateOnly(date),
        left,
        label: date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
      });
    }
    return ticks;
  }

  const ticks: TimelineTick[] = [];
  for (
    let date = new Date(rangeStart.getFullYear(), rangeStart.getMonth(), 1);
    date <= rangeEnd;
    date = new Date(date.getFullYear(), date.getMonth() + 1, 1)
  ) {
    ticks.push({
      key: toDateOnly(date),
      left: daysBetween(rangeStart, date) * PIXELS_PER_DAY.month,
      label: date.toLocaleDateString(undefined, { month: 'short', year: 'numeric' }),
    });
  }
  return ticks;
}

export interface TimelineMonthBand {
  key: string;
  left: number;
  width: number;
  label: string;
}

/** Only meaningful at Day/Week scale, where a single tick can't convey "which month" on its
 * own — Month scale's ticks already are months, so callers skip this at that scale. */
export function buildTimelineMonthBands(rangeStart: Date, rangeEnd: Date, pixelsPerDay: number): TimelineMonthBand[] {
  const bands: TimelineMonthBand[] = [];
  let cursor = new Date(rangeStart.getFullYear(), rangeStart.getMonth(), 1);

  while (cursor <= rangeEnd) {
    const monthStart = cursor < rangeStart ? rangeStart : cursor;
    const nextMonth = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 1);
    const monthEnd = addDays(nextMonth, -1) > rangeEnd ? rangeEnd : addDays(nextMonth, -1);

    bands.push({
      key: toDateOnly(cursor),
      left: daysBetween(rangeStart, monthStart) * pixelsPerDay,
      width: (daysBetween(monthStart, monthEnd) + 1) * pixelsPerDay,
      label: cursor.toLocaleDateString(undefined, { month: 'long', year: 'numeric' }),
    });

    cursor = nextMonth;
  }

  return bands;
}
