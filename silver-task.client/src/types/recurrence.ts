import type { TaskPriority } from './task';
import type { UserSummary } from './project';

export type RecurrenceFrequency = 'Daily' | 'Weekly' | 'Monthly' | 'Yearly';

export const RECURRENCE_FREQUENCY_OPTIONS: RecurrenceFrequency[] = ['Daily', 'Weekly', 'Monthly', 'Yearly'];

/** JS Date#getDay() order (Sunday-first) — matches the backend's System.DayOfWeek and this app's
 * own week-start convention (utils/calendarGrid.ts startOfWeek). */
export type WeekdayName = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';

export const WEEKDAY_OPTIONS: WeekdayName[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

export const WEEKDAY_LABELS: Record<WeekdayName, string> = {
  Sunday: 'Sun',
  Monday: 'Mon',
  Tuesday: 'Tue',
  Wednesday: 'Wed',
  Thursday: 'Thu',
  Friday: 'Fri',
  Saturday: 'Sat',
};

export type RecurrenceEditScope = 'ThisAndFuture' | 'EntireSeries';

export interface RecurrenceRule {
  id: string;
  projectId: string;
  parentTaskId: string | null;
  templateTaskId: string | null;
  templateTaskTitle: string | null;
  title: string;
  description: string | null;
  priority: TaskPriority;
  assignedTo: UserSummary | null;
  frequency: RecurrenceFrequency;
  interval: number;
  daysOfWeek: WeekdayName[];
  dayOfMonth: number | null;
  monthOfYear: number | null;
  startDate: string;
  endDate: string | null;
  maxOccurrences: number | null;
  occurrencesGenerated: number;
  nextOccurrenceDate: string | null;
  isActive: boolean;
  scheduleDescription: string;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
}

export interface RecurrenceRuleInput {
  title: string;
  description?: string;
  priority: TaskPriority;
  assignedToUserId?: string | null;
  frequency: RecurrenceFrequency;
  interval: number;
  daysOfWeek?: WeekdayName[];
  dayOfMonth?: number | null;
  monthOfYear?: number | null;
  startDate: string;
  endDate?: string | null;
  maxOccurrences?: number | null;
}

export type CreateRecurrenceRequest = RecurrenceRuleInput;

export interface UpdateRecurrenceRequest extends RecurrenceRuleInput {
  scope: RecurrenceEditScope;
  anchorOccurrenceDate?: string | null;
}
