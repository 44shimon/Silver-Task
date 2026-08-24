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
  createdAt: string;
  updatedAt: string;
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
}
