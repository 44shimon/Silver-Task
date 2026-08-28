import type { TaskPriority, TaskStatus } from './task';
import type { DependencyType } from './dependency';

export type TemplateType = 'Project' | 'Task';

/** Mirrors Silver-Task.Server/Common/TemplateAssignmentModes.cs. There is deliberately no "Keep"
 * value here — that's a UI-only concept (the wizard's global override defaulting to "use each
 * task's own template default"), not a stored mode. */
export type TemplateAssignmentMode = 'Unassigned' | 'ProjectManager' | 'SpecificUser';

export const TEMPLATE_ASSIGNMENT_MODE_OPTIONS: TemplateAssignmentMode[] = ['Unassigned', 'ProjectManager', 'SpecificUser'];

export const TEMPLATE_ASSIGNMENT_MODE_LABELS: Record<TemplateAssignmentMode, string> = {
  Unassigned: 'Leave Unassigned',
  ProjectManager: 'Assign to Project Manager',
  SpecificUser: 'Assign to Specific User',
};

export interface TemplateCustomValue {
  customFieldId: string;
  value: string | null;
}

export interface TemplateChecklistItem {
  id: string;
  text: string;
  sortOrder: number;
}

export interface TemplateSharedUser {
  userId: string;
  name: string;
}

/** The Template Home list row — one per template regardless of type. */
export interface TemplateSummary {
  id: string;
  type: TemplateType;
  name: string;
  description: string | null;
  createdByUserId: string;
  createdByName: string;
  isArchived: boolean;
  taskCount: number;
  usageCount: number;
  lastUsedAt: string | null;
  isOwnedByMe: boolean;
  isFavorite: boolean;
  createdAt: string;
  updatedAt: string;
}

// ---------- Project Templates ----------

export interface ProjectTemplateTask {
  id: string;
  parentTemplateTaskId: string | null;
  title: string;
  description: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  startOffsetDays: number | null;
  dueOffsetDays: number | null;
  estimatedDurationDays: number | null;
  assignmentMode: TemplateAssignmentMode;
  assignedToUserId: string | null;
  assignedToName: string | null;
  sortOrder: number;
  tags: string[];
  customValues: TemplateCustomValue[];
  checklistItems: TemplateChecklistItem[];
}

export interface ProjectTemplateDependency {
  id: string;
  templateTaskId: string;
  dependsOnTemplateTaskId: string;
  dependencyType: DependencyType;
}

export interface ProjectTemplate {
  id: string;
  name: string;
  description: string | null;
  createdByUserId: string;
  createdByName: string;
  isArchived: boolean;
  isPublic: boolean;
  usageCount: number;
  lastUsedAt: string | null;
  isOwnedByMe: boolean;
  isFavorite: boolean;
  sharedWith: TemplateSharedUser[] | null;
  tasks: ProjectTemplateTask[];
  dependencies: ProjectTemplateDependency[];
  createdAt: string;
  updatedAt: string;
}

// Save (create/update) — a full-resource replace of the task/dependency graph. ClientId/
// ParentClientId/*ClientId fields are IDs the FRONTEND mints (a fresh crypto.randomUUID() per new
// task, or the task's real persisted id if editing an existing one) purely to correlate parent/
// dependency references WITHIN one request — mirrors SaveProjectTemplateRequest on the backend.

export interface SaveProjectTemplateTaskRequest {
  clientId: string;
  parentClientId?: string;
  title: string;
  description?: string;
  status: TaskStatus;
  priority: TaskPriority;
  startOffsetDays?: number;
  dueOffsetDays?: number;
  estimatedDurationDays?: number;
  assignmentMode: TemplateAssignmentMode;
  assignedToUserId?: string;
  sortOrder: number;
  tags: string[];
  customValues: TemplateCustomValue[];
  checklistItems: string[];
}

export interface SaveProjectTemplateDependencyRequest {
  templateTaskClientId: string;
  dependsOnTemplateTaskClientId: string;
  dependencyType: DependencyType;
}

export interface SaveProjectTemplateRequest {
  name: string;
  description?: string;
  isPublic: boolean;
  tasks: SaveProjectTemplateTaskRequest[];
  dependencies: SaveProjectTemplateDependencyRequest[];
}

// ---------- Task Templates ----------

export interface TaskTemplate {
  id: string;
  name: string;
  description: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  startOffsetDays: number | null;
  dueOffsetDays: number | null;
  estimatedDurationDays: number | null;
  assignmentMode: TemplateAssignmentMode;
  assignedToUserId: string | null;
  assignedToName: string | null;
  createdByUserId: string;
  createdByName: string;
  isArchived: boolean;
  isPublic: boolean;
  usageCount: number;
  lastUsedAt: string | null;
  isOwnedByMe: boolean;
  isFavorite: boolean;
  sharedWith: TemplateSharedUser[] | null;
  tags: string[];
  customValues: TemplateCustomValue[];
  checklistItems: TemplateChecklistItem[];
  createdAt: string;
  updatedAt: string;
}

export interface SaveTaskTemplateRequest {
  name: string;
  description?: string;
  status: TaskStatus;
  priority: TaskPriority;
  startOffsetDays?: number;
  dueOffsetDays?: number;
  estimatedDurationDays?: number;
  assignmentMode: TemplateAssignmentMode;
  assignedToUserId?: string;
  isPublic: boolean;
  tags: string[];
  customValues: TemplateCustomValue[];
  checklistItems: string[];
}

// ---------- Sharing ----------

export interface ShareTemplateRequest {
  email: string;
}

// ---------- Instantiation ----------

export interface CreateProjectFromTemplateRequest {
  templateId: string;
  projectName: string;
  projectDescription?: string;
  startDate: string;
  /** null = use each task's own template default. SpecificUser is intentionally not a valid
   * override value here — see the backend DTO's own doc comment. */
  assignmentOverride?: 'Unassigned' | 'ProjectManager';
}

export interface CreateTaskFromTemplateRequest {
  templateId: string;
  projectId: string;
  startDateOverride?: string;
}

export interface TemplateScheduleItem {
  templateTaskId: string;
  title: string;
  computedStartDate: string | null;
  computedDueDate: string | null;
}

export interface ProjectTemplatePreview {
  templateName: string;
  taskCount: number;
  subtaskCount: number;
  dependencyCount: number;
  estimatedDurationDays: number | null;
  schedule: TemplateScheduleItem[];
  warnings: string[];
}
