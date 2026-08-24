import { useMemo, useState } from 'react';
import type { Task, TaskPriority, TaskStatus } from '@/types/task';
import type { SortDirection } from '@/hooks/useTaskFilters';
import { daysFromTodayDateOnly, todayDateOnly } from '@/utils/dateOnly';

export type MyTaskSortField = 'title' | 'project' | 'status' | 'priority' | 'dueDate' | 'createdAt' | 'updatedAt';

export const MY_TASK_SORT_FIELD_LABELS: Record<MyTaskSortField, string> = {
  title: 'Task',
  project: 'Project',
  status: 'Status',
  priority: 'Priority',
  dueDate: 'Due Date',
  createdAt: 'Created Date',
  updatedAt: 'Updated Date',
};

export const MY_TASK_SORT_FIELDS: MyTaskSortField[] = [
  'title',
  'project',
  'status',
  'priority',
  'dueDate',
  'createdAt',
  'updatedAt',
];

/** Mutually-exclusive quick filter, AND-combined with the detailed filters below — matches the
 * "All / Due Today / Due This Week / Overdue / Completed" quick-filter row plus summary cards. */
export type QuickFilter = 'all' | 'open' | 'dueToday' | 'dueThisWeek' | 'overdue' | 'completed';

export interface MyTasksFilters {
  projectId: string | null;
  status: TaskStatus | null;
  priority: TaskPriority | null;
  /** DateOnly ("YYYY-MM-DD"); matches tasks due strictly before this date. */
  dueBefore: string | null;
}

const EMPTY_FILTERS: MyTasksFilters = { projectId: null, status: null, priority: null, dueBefore: null };

const STATUS_RANK: Record<TaskStatus, number> = {
  NotStarted: 0,
  InProgress: 1,
  Waiting: 2,
  Blocked: 3,
  Complete: 4,
  Cancelled: 5,
};

const PRIORITY_RANK: Record<TaskPriority, number> = { Low: 0, Medium: 1, High: 2, Urgent: 3 };

/** Search + quick-filter + detailed-filter + sort over the already-loaded My Tasks list
 * (client-side, same reasoning as useTaskFilters — the list is already server-scoped to the
 * caller's own assignments, so there's nothing to gain from round-tripping per keystroke). */
export function useMyTasksFilters(tasks: Task[]) {
  const [searchQuery, setSearchQuery] = useState('');
  const [quickFilter, setQuickFilter] = useState<QuickFilter>('all');
  const [filters, setFilters] = useState<MyTasksFilters>(EMPTY_FILTERS);
  const [sortField, setSortFieldState] = useState<MyTaskSortField>('dueDate');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');

  function setSortField(field: MyTaskSortField) {
    if (field === sortField) {
      setSortDirection((dir) => (dir === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortFieldState(field);
      setSortDirection('asc');
    }
  }

  const activeFilterCount = [filters.projectId, filters.status, filters.priority, filters.dueBefore].filter(
    (value) => value !== null,
  ).length;

  const today = todayDateOnly();
  const weekEnd = daysFromTodayDateOnly(6);

  const summary = useMemo(() => {
    const isOpenTask = (task: Task) => task.status !== 'Complete' && task.status !== 'Cancelled';
    return {
      total: tasks.length,
      open: tasks.filter(isOpenTask).length,
      dueToday: tasks.filter((t) => t.dueDate === today && isOpenTask(t)).length,
      overdue: tasks.filter((t) => t.dueDate !== null && t.dueDate < today && isOpenTask(t)).length,
      completed: tasks.filter((t) => t.status === 'Complete').length,
    };
  }, [tasks, today]);

  const filteredTasks = useMemo(() => {
    let items = tasks;

    const query = searchQuery.trim().toLowerCase();
    if (query) {
      items = items.filter(
        (task) =>
          task.title.toLowerCase().includes(query) ||
          (task.description ?? '').toLowerCase().includes(query) ||
          (task.projectName ?? '').toLowerCase().includes(query),
      );
    }

    switch (quickFilter) {
      case 'open':
        items = items.filter((task) => task.status !== 'Complete' && task.status !== 'Cancelled');
        break;
      case 'dueToday':
        items = items.filter((task) => task.dueDate === today);
        break;
      case 'dueThisWeek':
        items = items.filter((task) => task.dueDate !== null && task.dueDate >= today && task.dueDate <= weekEnd);
        break;
      case 'overdue':
        items = items.filter(
          (task) => task.dueDate !== null && task.dueDate < today && task.status !== 'Complete' && task.status !== 'Cancelled',
        );
        break;
      case 'completed':
        items = items.filter((task) => task.status === 'Complete');
        break;
      case 'all':
        break;
    }

    if (filters.projectId) {
      items = items.filter((task) => task.projectId === filters.projectId);
    }
    if (filters.status) {
      items = items.filter((task) => task.status === filters.status);
    }
    if (filters.priority) {
      items = items.filter((task) => task.priority === filters.priority);
    }
    if (filters.dueBefore) {
      items = items.filter((task) => task.dueDate !== null && task.dueDate < filters.dueBefore!);
    }

    return [...items].sort((a, b) => {
      const result = compareTasks(a, b, sortField);
      return sortDirection === 'asc' ? result : -result;
    });
  }, [tasks, searchQuery, quickFilter, filters, sortField, sortDirection, today, weekEnd]);

  return {
    filteredTasks,
    isFiltered: tasks.length > 0 && (activeFilterCount > 0 || quickFilter !== 'all' || searchQuery.trim().length > 0),
    searchQuery,
    setSearchQuery,
    quickFilter,
    setQuickFilter,
    filters,
    setFilters,
    clearFilters: () => setFilters(EMPTY_FILTERS),
    activeFilterCount,
    sortField,
    sortDirection,
    setSortField,
    setSortDirection,
    summary,
  };
}

function compareTasks(a: Task, b: Task, field: MyTaskSortField): number {
  switch (field) {
    case 'title':
      return a.title.localeCompare(b.title);
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

function compareNullableDateStrings(a: string | null, b: string | null): number {
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
