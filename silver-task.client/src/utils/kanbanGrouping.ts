import { STATUS_LABELS, STATUS_OPTIONS, type Task } from '@/types/task';

export interface KanbanColumnDef {
  id: string;
  label: string;
}

export interface KanbanColumn extends KanbanColumnDef {
  tasks: Task[];
}

/**
 * Generic column-grouping over an already-filtered task list. Takes a key extractor so the
 * board can regroup by something other than Status later (Priority/Assigned User/Project, per
 * the "design the architecture so grouping can be extended later" requirement) without
 * changing how KanbanBoard renders — only what's passed in here.
 */
export function groupTasks(tasks: Task[], columns: KanbanColumnDef[], getGroupId: (task: Task) => string): KanbanColumn[] {
  return columns.map((column) => ({
    ...column,
    tasks: tasks.filter((task) => getGroupId(task) === column.id),
  }));
}

export const STATUS_KANBAN_COLUMNS: KanbanColumnDef[] = STATUS_OPTIONS.map((status) => ({
  id: status,
  label: STATUS_LABELS[status],
}));

/** The only grouping actually wired up this phase — Priority/Assigned User/Project grouping
 * can reuse `groupTasks` with a different column list + key extractor when that's built. */
export function groupTasksByStatus(tasks: Task[]): KanbanColumn[] {
  return groupTasks(tasks, STATUS_KANBAN_COLUMNS, (task) => task.status);
}
