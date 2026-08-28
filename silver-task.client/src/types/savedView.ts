import type { CustomFieldConditionOperator } from './customField';
import type { Task } from './task';
import type { Project } from './project';

export type SavedViewEntityType = 'Task' | 'Project';

export type SavedViewLayout = 'Table' | 'Kanban' | 'Calendar' | 'Timeline' | 'Gantt';

export const SAVED_VIEW_LAYOUTS: SavedViewLayout[] = ['Table', 'Kanban', 'Calendar', 'Timeline', 'Gantt'];

/** Mirrors Silver-Task.Server/Common/SavedViewTypes.cs's SavedViewFields. */
export const SAVED_VIEW_FIELDS = {
  status: 'status',
  priority: 'priority',
  assigneeId: 'assigneeId',
  projectId: 'projectId',
  tagId: 'tagId',
  dueDate: 'dueDate',
  createdAt: 'createdAt',
  updatedAt: 'updatedAt',
} as const;

export const CUSTOM_FIELD_PREFIX = 'customField:';

export const ASSIGNEE_ME = 'me';
export const ASSIGNEE_UNASSIGNED = 'unassigned';

export type RelativeDateToken = 'today' | 'tomorrow' | 'thisWeek' | 'nextWeek' | 'thisMonth' | 'overdue' | 'noDueDate';

export const RELATIVE_DATE_OPTIONS: RelativeDateToken[] = ['today', 'tomorrow', 'thisWeek', 'nextWeek', 'thisMonth', 'overdue', 'noDueDate'];

export const RELATIVE_DATE_LABELS: Record<RelativeDateToken, string> = {
  today: 'Today',
  tomorrow: 'Tomorrow',
  thisWeek: 'This Week',
  nextWeek: 'Next Week',
  thisMonth: 'This Month',
  overdue: 'Overdue',
  noDueDate: 'No Due Date',
};

export interface SavedViewFilterCondition {
  field: string;
  operator: CustomFieldConditionOperator;
  value: string | null;
  valueTo: string | null;
}

export interface SavedViewFilterGroup {
  logic: 'AND' | 'OR';
  conditions: SavedViewFilterCondition[];
  groups: SavedViewFilterGroup[];
}

export function emptyFilterGroup(): SavedViewFilterGroup {
  return { logic: 'AND', conditions: [], groups: [] };
}

export interface SavedViewSharedUser {
  userId: string;
  name: string;
}

export interface SavedView {
  id: string;
  name: string;
  description: string | null;
  createdByUserId: string;
  createdByName: string;
  entityType: SavedViewEntityType;
  isPublic: boolean;
  filter: SavedViewFilterGroup;
  columns: string[];
  sortField: string | null;
  sortDescending: boolean;
  groupByField: string | null;
  layout: SavedViewLayout;
  isOwnedByMe: boolean;
  isFavorite: boolean;
  favoriteSortOrder: number | null;
  isSystemDefault: boolean;
  sharedWith: SavedViewSharedUser[] | null;
  createdAt: string;
  updatedAt: string;
}

export interface SaveViewRequest {
  name: string;
  description?: string | null;
  entityType: SavedViewEntityType;
  isPublic: boolean;
  filter: SavedViewFilterGroup;
  columns?: string[];
  sortField?: string | null;
  sortDescending: boolean;
  groupByField?: string | null;
  layout: SavedViewLayout;
}

export interface ExecuteViewResult {
  tasks: Task[];
  projects: Project[];
  total: number;
  page: number;
  pageSize: number;
  resolvedSingleProjectId: string | null;
  unavailableFilterFields: string[];
}

export interface PreviewResult {
  total: number;
  resolvedSingleProjectId: string | null;
  unavailableFilterFields: string[];
}

export interface PreviewViewRequest {
  entityType: SavedViewEntityType;
  filter: SavedViewFilterGroup;
}
