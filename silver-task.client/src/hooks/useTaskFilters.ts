import { useMemo, useState } from 'react';
import type { Task, TaskPriority, TaskStatus } from '@/types/task';
import type { CustomField } from '@/types/customField';
import { taskMatchesQuery } from '@/utils/taskSearch';

export type TaskSortField = 'title' | 'assignedTo' | 'status' | 'priority' | 'dueDate' | 'createdAt' | 'updatedAt';

export type SortDirection = 'asc' | 'desc';

export interface TaskFilters {
  status: TaskStatus | null;
  priority: TaskPriority | null;
  /** A user id, the sentinel 'unassigned', or null for "anyone". */
  assigneeId: string | null;
  /** DateOnly ("YYYY-MM-DD"); matches tasks due strictly before this date. */
  dueBefore: string | null;
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

const STATUS_RANK: Record<TaskStatus, number> = {
  NotStarted: 0,
  InProgress: 1,
  Waiting: 2,
  Blocked: 3,
  Complete: 4,
  Cancelled: 5,
};

const PRIORITY_RANK: Record<TaskPriority, number> = { Low: 0, Medium: 1, High: 2, Urgent: 3 };

/** Client-side search + filter + sort over an already-loaded task list. The project
 * task list has no pagination yet (Phase 14), so there's nothing to gain from
 * round-tripping to the server for every keystroke or filter change. */
export function useTaskFilters(tasks: Task[], customFields: CustomField[] = []) {
  const [searchQuery, setSearchQuery] = useState('');
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

  const filteredTasks = useMemo(() => {
    let items = tasks;

    // Matches title/description/project/assigned-user/Text-LongText-custom-fields — shared
    // with useMyTasksFilters (and any future view) via taskMatchesQuery, not reimplemented here.
    items = items.filter((task) => taskMatchesQuery(task, searchQuery, textFieldIds));

    if (filters.status) {
      items = items.filter((task) => task.status === filters.status);
    }
    if (filters.priority) {
      items = items.filter((task) => task.priority === filters.priority);
    }
    if (filters.assigneeId === 'unassigned') {
      items = items.filter((task) => !task.assignedTo);
    } else if (filters.assigneeId) {
      items = items.filter((task) => task.assignedTo?.id === filters.assigneeId);
    }
    if (filters.dueBefore) {
      items = items.filter((task) => task.dueDate !== null && task.dueDate < filters.dueBefore!);
    }

    return [...items].sort((a, b) => {
      const result = compareTasks(a, b, sortField);
      return sortDirection === 'asc' ? result : -result;
    });
  }, [tasks, searchQuery, filters, sortField, sortDirection, textFieldIds]);

  return {
    filteredTasks,
    isFiltered: tasks.length > 0 && (activeFilterCount > 0 || searchQuery.trim().length > 0),
    searchQuery,
    setSearchQuery,
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

function compareTasks(a: Task, b: Task, field: TaskSortField): number {
  switch (field) {
    case 'title':
      return a.title.localeCompare(b.title);
    case 'assignedTo':
      return (a.assignedTo?.name ?? '').localeCompare(b.assignedTo?.name ?? '');
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

/** DateOnly/ISO timestamp strings are zero-padded and big-endian, so plain string
 * comparison is already chronological — no Date parsing needed. Nulls sort last. */
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
