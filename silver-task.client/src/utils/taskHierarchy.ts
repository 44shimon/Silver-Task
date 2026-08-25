import type { Task } from '@/types/task';
import { parseDateOnly } from './calendarGrid';

/** Walks parentTaskId links through an already-loaded task list to build the ancestor chain
 * (root-first, not including `task` itself) — no dedicated "ancestors" API endpoint needed since
 * the caller (ProjectPage/MyTasksPage) already has the full project task list loaded. Stops
 * early (rather than looping forever) if a parent link can't be resolved in `tasks`, e.g. a
 * cross-project edge case or a task filtered out of the caller's list. */
export function getTaskAncestors(task: Task, tasks: Task[]): Task[] {
  const byId = new Map(tasks.map((t) => [t.id, t]));
  const ancestors: Task[] = [];
  const visited = new Set<string>();

  let currentParentId = task.parentTaskId;
  while (currentParentId && !visited.has(currentParentId)) {
    visited.add(currentParentId);
    const parent = byId.get(currentParentId);
    if (!parent) {
      break;
    }
    ancestors.unshift(parent);
    currentParentId = parent.parentTaskId;
  }

  return ancestors;
}

/** Every descendant of `task` (not including task itself) from an already-loaded task list —
 * used to exclude invalid targets from the Move Task dialog client-side. The backend's own
 * ancestor walk on the actual move is still the authoritative circular-hierarchy check. */
export function getTaskDescendantIds(task: Task, tasks: Task[]): Set<string> {
  const childrenByParent = new Map<string, Task[]>();
  for (const t of tasks) {
    if (!t.parentTaskId) {
      continue;
    }
    const bucket = childrenByParent.get(t.parentTaskId);
    if (bucket) {
      bucket.push(t);
    } else {
      childrenByParent.set(t.parentTaskId, [t]);
    }
  }

  const descendants = new Set<string>();
  const stack = [...(childrenByParent.get(task.id) ?? [])];
  while (stack.length > 0) {
    const next = stack.pop()!;
    if (descendants.has(next.id)) {
      continue;
    }
    descendants.add(next.id);
    stack.push(...(childrenByParent.get(next.id) ?? []));
  }

  return descendants;
}

export interface TaskTreeNode extends Task {
  subRows: TaskTreeNode[];
}

/** Nests a flat task list into a tree by parentTaskId — the shape TanStack Table's built-in
 * getSubRows/getExpandedRowModel tree support expects. Deliberately does NOT re-sort siblings by
 * sortOrder: `tasks` arrives already sorted by whatever the Table's own "Sort by" menu currently
 * has active (title, due date, ...), and grouping while preserving that input order means each
 * sibling group stays consistent with the active sort instead of silently reverting to manual
 * order underneath it. A task whose parent isn't present in `tasks` (filtered out by search/
 * filters, e.g.) becomes its own root-level row rather than silently disappearing. */
export function buildTaskTree(tasks: Task[]): TaskTreeNode[] {
  const nodeById = new Map<string, TaskTreeNode>(tasks.map((t) => [t.id, { ...t, subRows: [] }]));
  const roots: TaskTreeNode[] = [];

  for (const task of tasks) {
    const node = nodeById.get(task.id)!;
    const parent = task.parentTaskId ? nodeById.get(task.parentTaskId) : undefined;
    if (parent) {
      parent.subRows.push(node);
    } else {
      roots.push(node);
    }
  }

  return roots;
}

/** Flattens a task list into hierarchy-ordered *visible* rows with depth info — for chart views
 * (Timeline/Gantt) that render their own rows rather than using TanStack Table's tree support.
 * Children of a collapsed id are skipped entirely (not just hidden), so callers never draw a bar
 * for a row that isn't actually visible. */
export interface HierarchyRow {
  task: Task;
  depth: number;
  hasChildren: boolean;
}

export function flattenVisibleHierarchy(tasks: Task[], collapsedIds: ReadonlySet<string>): HierarchyRow[] {
  const childrenByParent = new Map<string | null, Task[]>();
  for (const task of tasks) {
    const key = task.parentTaskId;
    const bucket = childrenByParent.get(key);
    if (bucket) {
      bucket.push(task);
    } else {
      childrenByParent.set(key, [task]);
    }
  }
  for (const bucket of childrenByParent.values()) {
    bucket.sort((a, b) => a.sortOrder - b.sortOrder);
  }

  const rows: HierarchyRow[] = [];
  function visit(parentId: string | null, depth: number) {
    const children = childrenByParent.get(parentId) ?? [];
    for (const task of children) {
      const hasChildren = childrenByParent.has(task.id);
      rows.push({ task, depth, hasChildren });
      if (hasChildren && !collapsedIds.has(task.id)) {
        visit(task.id, depth + 1);
      }
    }
  }
  visit(null, 0);

  return rows;
}

/** Orders a filtered (e.g. "has dates") task subset so each task follows its nearest scheduled
 * ancestor, computing depth relative to that subset — used by Timeline, which only renders rows
 * for tasks with their own dates but still wants subtasks visually grouped under a scheduled
 * parent when one exists. Walks through `allTasks` (not just `scheduledTasks`) to find the
 * nearest scheduled ancestor across unscheduled intermediate parents. Preserves `scheduledTasks`'
 * input order among siblings (same rationale as buildTaskTree — respects the view's active sort). */
export interface ScheduledHierarchyRow {
  task: Task;
  depth: number;
}

export function orderByScheduledHierarchy(scheduledTasks: Task[], allTasks: Task[]): ScheduledHierarchyRow[] {
  const allById = new Map(allTasks.map((t) => [t.id, t]));
  const scheduledIds = new Set(scheduledTasks.map((t) => t.id));

  function nearestScheduledAncestorId(task: Task): string | null {
    let current = task.parentTaskId ? allById.get(task.parentTaskId) : undefined;
    const visited = new Set<string>();
    while (current && !visited.has(current.id)) {
      if (scheduledIds.has(current.id)) {
        return current.id;
      }
      visited.add(current.id);
      current = current.parentTaskId ? allById.get(current.parentTaskId) : undefined;
    }
    return null;
  }

  const childrenByParent = new Map<string | null, Task[]>();
  for (const task of scheduledTasks) {
    const key = nearestScheduledAncestorId(task);
    const bucket = childrenByParent.get(key);
    if (bucket) {
      bucket.push(task);
    } else {
      childrenByParent.set(key, [task]);
    }
  }

  const rows: ScheduledHierarchyRow[] = [];
  function visitScheduled(parentId: string | null, depth: number) {
    for (const task of childrenByParent.get(parentId) ?? []) {
      rows.push({ task, depth });
      visitScheduled(task.id, depth + 1);
    }
  }
  visitScheduled(null, 0);

  return rows;
}

export interface GanttRow {
  task: Task;
  depth: number;
  hasChildren: boolean;
  range: { start: Date; end: Date };
  /** True when this row has no dates of its own and is only shown/positioned because at least
   * one descendant does — the range is computed for display only (earliest descendant start to
   * latest descendant due) and is never written back to the task's own fields. */
  isCalculated: boolean;
}

/** Builds the ordered, depth-annotated, collapse-aware row list Gantt renders — including parent
 * tasks that have no dates of their own but have at least one dated descendant. A task with no
 * dates and no dated descendants anywhere in its subtree is dropped entirely (shown in the
 * Unscheduled tray instead, same as Timeline). */
export function buildGanttRows(tasks: Task[], collapsedIds: ReadonlySet<string>): GanttRow[] {
  const byId = new Map(tasks.map((t) => [t.id, t]));
  const childrenByParent = new Map<string, Task[]>();
  const roots: Task[] = [];
  for (const task of tasks) {
    if (task.parentTaskId && byId.has(task.parentTaskId)) {
      const bucket = childrenByParent.get(task.parentTaskId);
      if (bucket) {
        bucket.push(task);
      } else {
        childrenByParent.set(task.parentTaskId, [task]);
      }
    } else {
      roots.push(task);
    }
  }

  function ownRange(task: Task): { start: Date; end: Date } | null {
    if (task.startDate === null && task.dueDate === null) {
      return null;
    }
    const start = parseDateOnly(task.startDate ?? task.dueDate!);
    const end = parseDateOnly(task.dueDate ?? task.startDate!);
    return start <= end ? { start, end } : { start: end, end: start };
  }

  const effectiveRangeCache = new Map<string, { start: Date; end: Date } | null>();
  function effectiveRange(task: Task): { start: Date; end: Date } | null {
    const cached = effectiveRangeCache.get(task.id);
    if (cached !== undefined) {
      return cached;
    }
    let range = ownRange(task);
    for (const child of childrenByParent.get(task.id) ?? []) {
      const childRange = effectiveRange(child);
      if (!childRange) {
        continue;
      }
      if (!range) {
        range = { start: childRange.start, end: childRange.end };
      } else {
        if (childRange.start < range.start) {
          range.start = childRange.start;
        }
        if (childRange.end > range.end) {
          range.end = childRange.end;
        }
      }
    }
    effectiveRangeCache.set(task.id, range);
    return range;
  }

  const rows: GanttRow[] = [];
  function visit(task: Task, depth: number) {
    const range = effectiveRange(task);
    if (!range) {
      return;
    }
    const children = childrenByParent.get(task.id) ?? [];
    rows.push({ task, depth, hasChildren: children.length > 0, range, isCalculated: ownRange(task) === null });
    if (children.length > 0 && !collapsedIds.has(task.id)) {
      for (const child of children) {
        visit(child, depth + 1);
      }
    }
  }
  for (const root of roots) {
    visit(root, 0);
  }

  return rows;
}
