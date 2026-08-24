import { useMemo, useState } from 'react';
import type { Task } from '@/types/task';
import type { CustomField } from '@/types/customField';
import { taskMatchesQuery } from '@/utils/taskSearch';
import { daysFromTodayDateOnly, todayDateOnly } from '@/utils/dateOnly';
import {
  compareTasksByField,
  matchesCommonFilters,
  matchesQuickFilter,
  type CommonSortField,
  type CommonTaskFilters,
  type QuickFilter,
  type SortDirection,
} from '@/utils/taskFilters';

export type TaskSortField = Exclude<CommonSortField, 'project'>;

export interface TaskFilters extends CommonTaskFilters {
  /** A user id, the sentinel 'unassigned', or null for "anyone". */
  assigneeId: string | null;
}

const EMPTY_FILTERS: TaskFilters = { status: null, priority: null, assigneeId: null, dueBefore: null };

export const SORT_FIELD_LABELS: Record<TaskSortField, string> = {
  title: 'Task',
  assignedTo: 'Assigned To',
  status: 'Status',
  priority: 'Priority',
  dueDate: 'Due Date',
  createdAt: 'Created Date',
  updatedAt: 'Updated Date',
};

export const SORT_FIELDS: TaskSortField[] = [
  'title',
  'assignedTo',
  'status',
  'priority',
  'dueDate',
  'createdAt',
  'updatedAt',
];

/** Search + quick-filter + detailed-filter + sort over an already-loaded task list. The project
 * task list has no pagination yet, so there's nothing to gain from round-tripping to the server
 * for every keystroke or filter change. Shares its quick-filter/common-filter/sort logic with
 * useMyTasksFilters via utils/taskFilters — only the Assignee dimension (meaningless in My
 * Tasks) and search's custom-field pass (meaningless across projects with different field
 * schemas) are specific to this hook. */
export function useTaskFilters(tasks: Task[], customFields: CustomField[] = []) {
  const [searchQuery, setSearchQuery] = useState('');
  const [quickFilter, setQuickFilter] = useState<QuickFilter>('all');
  const [filters, setFilters] = useState<TaskFilters>(EMPTY_FILTERS);
  const [sortField, setSortFieldState] = useState<TaskSortField>('title');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');

  // Custom text fields are searchable too, per spec — track which field ids are
  // Text/LongText so the search pass below knows which custom values to check.
  const textFieldIds = useMemo(
    () => new Set(customFields.filter((f) => f.fieldType === 'Text' || f.fieldType === 'LongText').map((f) => f.id)),
    [customFields],
  );

  // Clicking a column header (or picking the same field in the Sort menu) toggles
  // direction instead of resetting to ascending, matching typical spreadsheet behavior.
  function setSortField(field: TaskSortField) {
    if (field === sortField) {
      setSortDirection((dir) => (dir === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortFieldState(field);
      setSortDirection('asc');
    }
  }

  const activeFilterCount = [filters.status, filters.priority, filters.assigneeId, filters.dueBefore].filter(
    (value) => value !== null,
  ).length;

  const today = todayDateOnly();
  const weekEnd = daysFromTodayDateOnly(6);

  const filteredTasks = useMemo(() => {
    let items = tasks;

    // Matches title/description/project/assigned-user/Text-LongText-custom-fields — shared
    // with useMyTasksFilters (and any future view) via taskMatchesQuery, not reimplemented here.
    items = items.filter((task) => taskMatchesQuery(task, searchQuery, textFieldIds));

    items = items.filter((task) => matchesQuickFilter(task, quickFilter, today, weekEnd));
    items = items.filter((task) => matchesCommonFilters(task, filters));

    if (filters.assigneeId === 'unassigned') {
      items = items.filter((task) => !task.assignedTo);
    } else if (filters.assigneeId) {
      items = items.filter((task) => task.assignedTo?.id === filters.assigneeId);
    }

    return [...items].sort((a, b) => {
      const result = compareTasksByField(a, b, sortField);
      return sortDirection === 'asc' ? result : -result;
    });
  }, [tasks, searchQuery, quickFilter, filters, sortField, sortDirection, textFieldIds, today, weekEnd]);

  return {
    filteredTasks,
    isFiltered:
      tasks.length > 0 && (activeFilterCount > 0 || quickFilter !== 'all' || searchQuery.trim().length > 0),
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
  };
}
