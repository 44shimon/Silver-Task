import type { TaskPriority, TaskStatus } from './task';
import type { UserSummary } from './project';

/** Mirrors Silver-Task.Server/Common/DependencyTypes.cs. Default is FinishToStart. */
export type DependencyType = 'FinishToStart' | 'StartToStart' | 'FinishToFinish' | 'StartToFinish';

export const DEPENDENCY_TYPE_OPTIONS: DependencyType[] = ['FinishToStart', 'StartToStart', 'FinishToFinish', 'StartToFinish'];

export const DEPENDENCY_TYPE_LABELS: Record<DependencyType, string> = {
  FinishToStart: 'Finish → Start',
  StartToStart: 'Start → Start',
  FinishToFinish: 'Finish → Finish',
  StartToFinish: 'Start → Finish',
};

export const DEPENDENCY_TYPE_DESCRIPTIONS: Record<DependencyType, string> = {
  FinishToStart: 'The dependent task cannot start until the prerequisite is complete.',
  StartToStart: 'The dependent task can start once the prerequisite has started.',
  FinishToFinish: 'The dependent task cannot be completed until the prerequisite is complete.',
  StartToFinish: 'The dependent task cannot be completed until the prerequisite has started.',
};

/** One row in either the "Depends On" list or the "Blocking" list — DependencyId is the
 * TaskDependency row (needed to remove it); everything else describes the *other* task in the
 * relationship (the prerequisite for "Depends On", the dependent for "Blocking"). */
export interface TaskDependency {
  dependencyId: string;
  dependencyType: DependencyType;
  /** Whether THIS relationship's own condition currently holds (see
   * TaskDependencyService.IsRelationshipSatisfied on the backend) — backs the row's
   * satisfied/unsatisfied checkbox indicator. */
  isSatisfied: boolean;
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
