import type { TaskPriority, TaskStatus } from './task';
import type { UserSummary } from './project';

/** One row in either the "Depends On" list or the "Blocking" list — DependencyId is the
 * TaskDependency row (needed to remove it); everything else describes the *other* task in the
 * relationship (the prerequisite for "Depends On", the dependent for "Blocking"). */
export interface TaskDependency {
  dependencyId: string;
  dependencyType: string;
  createdAt: string;
  taskId: string;
  title: string;
  status: TaskStatus;
  priority: TaskPriority;
  assignedTo: UserSummary | null;
  dueDate: string | null;
}

/** A bare graph edge (no task details — consumers like the Gantt/Timeline views already have
 * full Task objects for every row) backing project-wide dependency-line rendering. */
export interface TaskDependencyEdge {
  taskId: string;
  dependsOnTaskId: string;
}
