import type { Task, TaskPriority, TaskStatus } from '@/types/task';

export type SortDirection = 'asc' | 'desc';

/** Every sort field any view actually uses. Each hook derives its own narrower field union
 * from this (Exclude<CommonSortField, 'project'>, etc.) instead of redeclaring the list, so the
 * comparator below can stay a single implementation shared by every "Sort by" menu. */
export type CommonSortField =
  | 'title'
  | 'assignedTo'
  | 'project'
  | 'status'
  | 'priority'
  | 'dueDate'
  | 'createdAt'
  | 'updatedAt';

const STATUS_RANK: Record<TaskStatus, number> = {
  NotStarted: 0,
  InProgress: 1,
  Waiting: 2,
  Blocked: 3,
  Complete: 4,
  Cancelled: 5,
};

const PRIORITY_RANK: Record<TaskPriority, number> = { Low: 0, Medium: 1, High: 2, Urgent: 3 };

/** DateOnly/ISO timestamp strings are zero-padded and big-endian, so plain string comparison
 * is already chronological — no Date parsing needed. Nulls sort last regardless of direction. */
export function compareNullableDateStrings(a: string | null, b: string | null): number {
  if (a === null && b === null) {
    return 0;
  }
  if (a === null) {
    return 1;
  }
  if (b === null) {
    return -1;
  }
  return a.localeCompare(b);
}

export function compareTasksByField(a: Task, b: Task, field: CommonSortField): number {
  switch (field) {
    case 'title':
      return a.title.localeCompare(b.title);
    case 'assignedTo':
      return (a.assignedTo?.name ?? '').localeCompare(b.assignedTo?.name ?? '');
    case 'project':
      return (a.projectName ?? '').localeCompare(b.projectName ?? '');
    case 'status':
      return STATUS_RANK[a.status] - STATUS_RANK[b.status];
    case 'priority':
      return PRIORITY_RANK[a.priority] - PRIORITY_RANK[b.priority];
    case 'dueDate':
      return compareNullableDateStrings(a.dueDate, b.dueDate);
    case 'createdAt':
      return a.createdAt.localeCompare(b.createdAt);
    case 'updatedAt':
      return a.updatedAt.localeCompare(b.updatedAt);
    default:
      return 0;
  }
}

/** Mutually-exclusive quick filter, AND-combined with whatever detailed filters a view also
 * has. Shared by the Project views (Table/Kanban/Calendar/Timeline/Gantt, via useTaskFilters)
 * and My Tasks (useMyTasksFilters) — same five chips, same meaning, everywhere. */
export type QuickFilter = 'all' | 'open' | 'dueToday' | 'dueThisWeek' | 'overdue' | 'completed';

export function isTaskOpen(task: Task): boolean {
  return task.status !== 'Complete' && task.status !== 'Cancelled';
}

export function matchesQuickFilter(task: Task, quickFilter: QuickFilter, today: string, weekEnd: string): boolean {
  switch (quickFilter) {
    case 'open':
      return isTaskOpen(task);
    case 'dueToday':
      return task.dueDate === today;
    case 'dueThisWeek':
      return task.dueDate !== null && task.dueDate >= today && task.dueDate <= weekEnd;
    case 'overdue':
      return task.dueDate !== null && task.dueDate < today && isTaskOpen(task);
    case 'completed':
      return task.status === 'Complete';
    case 'all':
      return true;
  }
}

/** Dependency-state axis, layered into the same shared CommonTaskFilters/matchesCommonFilters
 * every view already uses for Status/Priority/Due-before, rather than a parallel filter system. */
export type DependencyStateFilter = 'blocked' | 'notBlocked' | 'hasDependencies' | 'hasDependents';

export const DEPENDENCY_STATE_LABELS: Record<DependencyStateFilter, string> = {
  blocked: 'Blocked',
  notBlocked: 'Not blocked',
  hasDependencies: 'Has dependencies',
  hasDependents: 'Has dependents',
};

/** The filter dimensions every view supports (Status/Priority/Due-before/Dependency state).
 * Assignee (project views only — meaningless in My Tasks, which is always "assigned to me") and
 * Project (My Tasks only — meaningless within a single project's own views) are each layered on
 * top by whichever hook actually needs that one extra dimension, rather than forced into one
 * combined shape. */
/** Whether a task is part of a recurring series at all — not further split into "active series"
 * vs. "stopped series" here, since that state lives on the RecurringTask rule, not the Task
 * itself, and this filter operates over an already-loaded flat task list with no per-task rule
 * data attached. "Active/stopped" is instead answerable from the Recurring Tasks management list. */
export type RecurrenceStateFilter = 'recurring' | 'notRecurring';

export const RECURRENCE_STATE_LABELS: Record<RecurrenceStateFilter, string> = {
  recurring: 'Recurring',
  notRecurring: 'Not recurring',
};

export interface CommonTaskFilters {
  status: TaskStatus | null;
  priority: TaskPriority | null;
  /** DateOnly ("YYYY-MM-DD"); matches tasks due strictly before this date. */
  dueBefore: string | null;
  dependencyState: DependencyStateFilter | null;
  recurrenceState: RecurrenceStateFilter | null;
}

export function matchesCommonFilters(task: Task, filters: CommonTaskFilters): boolean {
  if (filters.status && task.status !== filters.status) {
    return false;
  }
  if (filters.priority && task.priority !== filters.priority) {
    return false;
  }
  if (filters.dueBefore && (task.dueDate === null || task.dueDate >= filters.dueBefore)) {
    return false;
  }
  switch (filters.dependencyState) {
    case 'blocked':
      if (task.blockedByCount === 0) return false;
      break;
    case 'notBlocked':
      if (task.blockedByCount > 0) return false;
      break;
    case 'hasDependencies':
      if (task.dependsOnCount === 0) return false;
      break;
    case 'hasDependents':
      if (task.dependentCount === 0) return false;
      break;
  }
  switch (filters.recurrenceState) {
    case 'recurring':
      if (task.recurringTaskId === null) return false;
      break;
    case 'notRecurring':
      if (task.recurringTaskId !== null) return false;
      break;
  }
  return true;
}
