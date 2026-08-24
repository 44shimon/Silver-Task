import type { Task } from '@/types/task';

/**
 * Case-insensitive partial match across every field the search spec calls for: title,
 * description, project name, assigned user, and Text/LongText custom field values. Shared by
 * every view's client-side search (useTaskFilters, useMyTasksFilters, and any future view) so
 * there's exactly one search implementation instead of one per view.
 */
export function taskMatchesQuery(task: Task, query: string, textFieldIds: ReadonlySet<string> = new Set()): boolean {
  const q = query.trim().toLowerCase();
  if (!q) {
    return true;
  }

  if (task.title.toLowerCase().includes(q)) {
    return true;
  }
  if ((task.description ?? '').toLowerCase().includes(q)) {
    return true;
  }
  if ((task.projectName ?? '').toLowerCase().includes(q)) {
    return true;
  }
  if ((task.assignedTo?.name ?? '').toLowerCase().includes(q)) {
    return true;
  }
  return task.customValues.some(
    (v) => textFieldIds.has(v.customFieldId) && (v.value ?? '').toLowerCase().includes(q),
  );
}
