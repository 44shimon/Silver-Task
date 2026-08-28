import type { UserSummary } from './project';

export type TaskStatus = 'NotStarted' | 'InProgress' | 'Waiting' | 'Blocked' | 'Complete' | 'Cancelled';

export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Urgent';

export const STATUS_OPTIONS: TaskStatus[] = ['NotStarted', 'InProgress', 'Waiting', 'Blocked', 'Complete', 'Cancelled'];

export const STATUS_LABELS: Record<TaskStatus, string> = {
  NotStarted: 'Not Started',
  InProgress: 'In Progress',
  Waiting: 'Waiting',
  Blocked: 'Blocked',
  Complete: 'Complete',
  Cancelled: 'Cancelled',
};

export const PRIORITY_OPTIONS: TaskPriority[] = ['Low', 'Medium', 'High', 'Urgent'];

export interface TaskCustomValue {
  customFieldId: string;
  value: string | null;
}

export interface Task {
  id: string;
  projectId: string;
  /** Only populated by cross-project endpoints (e.g. My Tasks) — null from the per-project list. */
  projectName: string | null;
  title: string;
  description: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  assignedTo: UserSummary | null;
  startDate: string | null;
  dueDate: string | null;
  completedAt: string | null;
  sortOrder: number;
  customValues: TaskCustomValue[];
  /** How many other tasks this one depends on (its prerequisites). */
  dependsOnCount: number;
  /** Of dependsOnCount, how many prerequisites are not yet Complete — 0 means this task isn't
   * currently dependency-blocked. Never reflected in `status`; purely a computed display value. */
  blockedByCount: number;
  /** How many other tasks depend on this one (the "Blocking" count). */
  dependentCount: number;
  /** Null means top-level. */
  parentTaskId: string | null;
  /** Only populated when parentTaskId is set — lets a cross-project list like My Tasks show
   * "Parent: X" without that other project's full task list already being loaded. */
  parentTaskTitle: string | null;
  /** Direct children count — not the full recursive subtree. */
  subtaskCount: number;
  /** Of subtaskCount, how many direct children have status === 'Complete'. */
  completedSubtaskCount: number;
  /** Set on every occurrence of a recurring series, including the first (the task recurrence was
   * originally attached to) — null for an ordinary task. */
  recurringTaskId: string | null;
  /** The calendar date this occurrence represents per the recurrence rule — not necessarily equal
   * to startDate/dueDate, since a single occurrence can be freely rescheduled. */
  recurrenceOccurrenceDate: string | null;
  /** 1-based position in the series (1 = the first/template occurrence). Display only. */
  occurrenceNumber: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface TaskChecklistItem {
  id: string;
  text: string;
  isChecked: boolean;
  sortOrder: number;
}

export interface CreateTaskRequest {
  title: string;
  description?: string;
  status?: TaskStatus;
  priority?: TaskPriority;
  assignedToUserId?: string;
  startDate?: string;
  dueDate?: string;
}

export interface UpdateTaskRequest {
  title: string;
  description?: string;
  status: TaskStatus;
  priority: TaskPriority;
  assignedToUserId?: string | null;
  startDate?: string | null;
  dueDate?: string | null;
  sortOrder: number;
  /** Phase 39 — set alongside overrideReason to bypass a dependency block; ignored unless the
   * status change is actually blocked. Requires Permissions.DependenciesOverride. */
  overrideDependencyBlock?: boolean;
  overrideReason?: string;
}
