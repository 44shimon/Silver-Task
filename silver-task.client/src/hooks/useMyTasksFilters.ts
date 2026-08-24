import { useMemo, useState } from 'react';
import type { Task } from '@/types/task';
import { daysFromTodayDateOnly, todayDateOnly } from '@/utils/dateOnly';
import { taskMatchesQuery } from '@/utils/taskSearch';
import {
  compareTasksByField,
  isTaskOpen,
  matchesCommonFilters,
  matchesQuickFilter,
  type CommonSortField,
  type CommonTaskFilters,
  type QuickFilter,
  type SortDirection,
} from '@/utils/taskFilters';

export type MyTaskSortField = Exclude<CommonSortField, 'assignedTo'>;

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

export interface MyTasksFilters extends CommonTaskFilters {
  projectId: string | null;
}

const EMPTY_FILTERS: MyTasksFilters = { projectId: null, status: null, priority: null, dueBefore: null };

/** Search + quick-filter + detailed-filter + sort over the already-loaded My Tasks list
 * (client-side, same reasoning as useTaskFilters — the list is already server-scoped to the
 * caller's own assignments, so there's nothing to gain from round-tripping per keystroke).
 * Shares its quick-filter/common-filter/sort logic with useTaskFilters via utils/taskFilters —
 * only the Project dimension (meaningless within a single project's own views) is specific to
 * this hook. */
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

  const summary = useMemo(
    () => ({
      total: tasks.length,
      open: tasks.filter(isTaskOpen).length,
      dueToday: tasks.filter((t) => t.dueDate === today && isTaskOpen(t)).length,
      overdue: tasks.filter((t) => t.dueDate !== null && t.dueDate < today && isTaskOpen(t)).length,
      completed: tasks.filter((t) => t.status === 'Complete').length,
    }),
    [tasks, today],
  );

  const filteredTasks = useMemo(() => {
    let items = tasks;

    // Same shared matcher as useTaskFilters — title/description/project/assigned-user (no
    // custom-field pass here, since My Tasks spans projects with different field schemas).
    items = items.filter((task) => taskMatchesQuery(task, searchQuery));

    items = items.filter((task) => matchesQuickFilter(task, quickFilter, today, weekEnd));
    items = items.filter((task) => matchesCommonFilters(task, filters));

    if (filters.projectId) {
      items = items.filter((task) => task.projectId === filters.projectId);
    }

    return [...items].sort((a, b) => {
      const result = compareTasksByField(a, b, sortField);
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
