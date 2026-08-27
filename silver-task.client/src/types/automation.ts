import type { UserSummary } from './project';

/** Mirrors Silver-Task.Server/Models/Entities/Enums/AutomationTriggerType.cs — a closed set, not
 * an open string-constant list, since a trigger always implies real engine support. */
export type AutomationTriggerType =
  | 'TaskCreated'
  | 'TaskUpdated'
  | 'TaskCompleted'
  | 'TaskReopened'
  | 'TaskAssigned'
  | 'TaskOverdue'
  | 'CommentAdded'
  | 'FileUploaded'
  | 'FileTagged'
  | 'SubtaskCompleted'
  | 'ProjectCreated'
  | 'TaskBecameReady'
  | 'TaskBecameBlocked'
  | 'DependencyAdded'
  | 'DependencyRemoved'
  | 'DependencyCompleted'
  | 'DependencyOverridden';

export const TRIGGER_TYPE_OPTIONS: AutomationTriggerType[] = [
  'TaskCreated',
  'TaskUpdated',
  'TaskCompleted',
  'TaskReopened',
  'TaskAssigned',
  'TaskOverdue',
  'CommentAdded',
  'FileUploaded',
  'FileTagged',
  'SubtaskCompleted',
  'ProjectCreated',
  'TaskBecameReady',
  'TaskBecameBlocked',
  'DependencyAdded',
  'DependencyRemoved',
  'DependencyCompleted',
  'DependencyOverridden',
];

export const TRIGGER_TYPE_LABELS: Record<AutomationTriggerType, string> = {
  TaskCreated: 'Task Created',
  TaskUpdated: 'Task Updated',
  TaskCompleted: 'Task Completed',
  TaskReopened: 'Task Reopened',
  TaskAssigned: 'Task Assigned',
  TaskOverdue: 'Task Becomes Overdue',
  CommentAdded: 'Comment Added',
  FileUploaded: 'File Uploaded',
  FileTagged: 'File Tagged',
  SubtaskCompleted: 'Subtask Completed',
  ProjectCreated: 'Project Created',
  TaskBecameReady: 'Task Became Ready',
  TaskBecameBlocked: 'Task Became Blocked',
  DependencyAdded: 'Dependency Added',
  DependencyRemoved: 'Dependency Removed',
  DependencyCompleted: 'Dependency Completed',
  DependencyOverridden: 'Dependency Overridden',
};

/** Mirrors AutomationConditionOperator.cs — Before/After are offered as separate, clearer
 * options for date fields even though they behave identically to GreaterThan/LessThan. */
export type AutomationConditionOperator =
  | 'Equals'
  | 'NotEquals'
  | 'Contains'
  | 'NotContains'
  | 'IsEmpty'
  | 'IsNotEmpty'
  | 'GreaterThan'
  | 'LessThan'
  | 'GreaterThanOrEqual'
  | 'LessThanOrEqual'
  | 'Before'
  | 'After';

export const OPERATOR_OPTIONS: AutomationConditionOperator[] = [
  'Equals',
  'NotEquals',
  'Contains',
  'NotContains',
  'IsEmpty',
  'IsNotEmpty',
  'GreaterThan',
  'LessThan',
  'GreaterThanOrEqual',
  'LessThanOrEqual',
  'Before',
  'After',
];

export const OPERATOR_LABELS: Record<AutomationConditionOperator, string> = {
  Equals: 'Equals',
  NotEquals: 'Does not equal',
  Contains: 'Contains',
  NotContains: 'Does not contain',
  IsEmpty: 'Is empty',
  IsNotEmpty: 'Is not empty',
  GreaterThan: 'Greater than',
  LessThan: 'Less than',
  GreaterThanOrEqual: 'Greater than or equal to',
  LessThanOrEqual: 'Less than or equal to',
  Before: 'Before',
  After: 'After',
};

/** Operators that take no value (e.g. "Is empty") — the builder hides the value input for these. */
export const OPERATORS_WITHOUT_VALUE = new Set<AutomationConditionOperator>(['IsEmpty', 'IsNotEmpty']);

/** Mirrors AutomationActionType.cs — the complete, closed set; deliberately excludes anything
 * destructive or cross-project. */
export type AutomationActionType =
  | 'AssignTask'
  | 'ChangeStatus'
  | 'ChangePriority'
  | 'AddLabel'
  | 'RemoveLabel'
  | 'SetDueDate'
  | 'SetStartDate'
  | 'AddComment'
  | 'CreateTask'
  | 'SendNotification'
  | 'AddFileTag';

export const ACTION_TYPE_OPTIONS: AutomationActionType[] = [
  'AssignTask',
  'ChangeStatus',
  'ChangePriority',
  'AddLabel',
  'RemoveLabel',
  'SetDueDate',
  'SetStartDate',
  'AddComment',
  'CreateTask',
  'SendNotification',
  'AddFileTag',
];

export const ACTION_TYPE_LABELS: Record<AutomationActionType, string> = {
  AssignTask: 'Assign Task',
  ChangeStatus: 'Change Status',
  ChangePriority: 'Change Priority',
  AddLabel: 'Add Label',
  RemoveLabel: 'Remove Label',
  SetDueDate: 'Set Due Date',
  SetStartDate: 'Set Start Date',
  AddComment: 'Add Comment',
  CreateTask: 'Create Task',
  SendNotification: 'Send Notification',
  AddFileTag: 'Add File Tag',
};

/** Mirrors AutomationUserSelector.cs — reused by AssignTask/CreateTask/SendNotification. None is
 * only valid for CreateTask (leave unassigned) and SendNotification (notify no one). */
export type AutomationUserSelector = 'None' | 'TaskAssignee' | 'ProjectManager' | 'SpecificUser';

export const USER_SELECTOR_LABELS: Record<AutomationUserSelector, string> = {
  None: 'No one',
  TaskAssignee: "The task's current assignee",
  ProjectManager: 'The project manager',
  SpecificUser: 'A specific person...',
};

export type AutomationExecutionStatus = 'Success' | 'Failed' | 'Skipped';

/** Mirrors Common/Automation/AutomationFields.cs's plain-string field keys — condition fields
 * differ per trigger type and span Task/File/Project "namespaces", so this is a lookup table, not
 * a closed union. Custom fields append "Task.CustomField:{fieldId}" dynamically. */
export const TASK_FIELDS = [
  'Task.Title',
  'Task.Description',
  'Task.Status',
  'Task.Priority',
  'Task.AssigneeId',
  'Task.CreatorId',
  'Task.DueDate',
  'Task.StartDate',
  'Task.ProjectId',
  'Task.ParentTaskId',
  'Task.Labels',
] as const;

export const FILE_FIELDS = [
  'File.FileName',
  'File.CategoryId',
  'File.Tags',
  'File.FileType',
  'File.UploadedByUserId',
  'File.ProjectId',
  'File.TaskId',
] as const;

export const PROJECT_FIELDS = ['Project.Name', 'Project.Status', 'Project.OwnerId'] as const;

export const SUBTASK_ALL_COMPLETE_FIELD = 'Task.AllSiblingSubtasksComplete';

export const CUSTOM_FIELD_PREFIX = 'Task.CustomField:';

export const FIELD_LABELS: Record<string, string> = {
  'Task.Title': 'Title',
  'Task.Description': 'Description',
  'Task.Status': 'Status',
  'Task.Priority': 'Priority',
  'Task.AssigneeId': 'Assignee',
  'Task.CreatorId': 'Creator',
  'Task.DueDate': 'Due Date',
  'Task.StartDate': 'Start Date',
  'Task.ProjectId': 'Project',
  'Task.ParentTaskId': 'Parent Task',
  'Task.Labels': 'Labels',
  [SUBTASK_ALL_COMPLETE_FIELD]: 'All subtasks complete',
  'File.FileName': 'Filename',
  'File.CategoryId': 'Category',
  'File.Tags': 'Tags',
  'File.FileType': 'File Type',
  'File.UploadedByUserId': 'Uploaded By',
  'File.ProjectId': 'Project',
  'File.TaskId': 'Task',
  'Project.Name': 'Name',
  'Project.Status': 'Status',
  'Project.OwnerId': 'Owner',
};

/** The non-custom-field options a builder should offer for a given trigger — mirrors
 * AutomationFields.GetApplicableFields exactly. Custom field options are appended separately by
 * the caller (AutomationBuilder), which has the project's own custom field list. */
export function getApplicableFields(triggerType: AutomationTriggerType): string[] {
  switch (triggerType) {
    case 'FileUploaded':
    case 'FileTagged':
      return [...FILE_FIELDS];
    case 'ProjectCreated':
      return [...PROJECT_FIELDS];
    case 'SubtaskCompleted':
      return [...TASK_FIELDS, SUBTASK_ALL_COMPLETE_FIELD];
    default:
      return [...TASK_FIELDS];
  }
}

export interface AutomationCondition {
  id: string;
  field: string;
  operator: AutomationConditionOperator;
  value: string | null;
}

export interface AutomationAction {
  id: string;
  actionType: AutomationActionType;
  /** Raw JSON object — type-specific parameters, see AutomationActionParameters below for the
   * shape per actionType (mirrors Silver-Task.Server/Models/AutomationParameters/ActionParameters.cs). */
  parameters: Record<string, unknown>;
}

export interface Automation {
  id: string;
  name: string;
  description: string | null;
  /** Null = global automation (Administrator-only, applies system-wide). */
  projectId: string | null;
  triggerType: AutomationTriggerType;
  isActive: boolean;
  conditions: AutomationCondition[];
  actions: AutomationAction[];
  createdBy: UserSummary;
  createdAt: string;
  updatedAt: string;
  lastRunAt: string | null;
  runCount: number;
  lastError: string | null;
}

export interface AutomationConditionRequest {
  field: string;
  operator: AutomationConditionOperator;
  value?: string | null;
}

export interface AutomationActionRequest {
  actionType: AutomationActionType;
  parameters: Record<string, unknown>;
}

export interface SaveAutomationRequest {
  name: string;
  description?: string;
  /** Set server-side from the URL for project-scoped/global create endpoints; present here only
   * because the same request shape round-trips through both. */
  projectId?: string | null;
  triggerType: AutomationTriggerType;
  isActive: boolean;
  conditions: AutomationConditionRequest[];
  actions: AutomationActionRequest[];
}

export interface AutomationExecution {
  id: string;
  automationId: string;
  automationName: string;
  triggerType: AutomationTriggerType;
  entityId: string | null;
  status: AutomationExecutionStatus;
  startedAt: string;
  completedAt: string | null;
  durationMs: number | null;
  errorMessage: string | null;
  resultSummary: string | null;
  retryOfExecutionId: string | null;
}

export interface AutomationExecutionList {
  items: AutomationExecution[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AutomationTestResult {
  conditionsMatched: boolean;
  actionPreviews: string[];
  explanation: string | null;
}

export interface AutomationQueryParams {
  search?: string;
  triggerType?: AutomationTriggerType;
  isActive?: boolean;
  createdByUserId?: string;
}

/** Type-specific parameter shapes, one per AutomationActionType — mirrors
 * Silver-Task.Server/Models/AutomationParameters/ActionParameters.cs. The builder reads/writes
 * these as plain objects (AutomationAction.parameters is untyped JSON on the wire). */
export interface AssignTaskParameters {
  assignMode: AutomationUserSelector;
  targetUserId?: string | null;
}

export interface ChangeStatusParameters {
  newStatus: string;
}

export interface ChangePriorityParameters {
  newPriority: string;
}

export interface AddLabelParameters {
  tagName: string;
}

export interface RemoveLabelParameters {
  tagName: string;
}

export interface SetDueDateParameters {
  offsetDays?: number | null;
  clearDate: boolean;
}

export interface SetStartDateParameters {
  offsetDays?: number | null;
  clearDate: boolean;
}

export interface AddCommentParameters {
  commentTemplate: string;
}

export interface CreateTaskParameters {
  titleTemplate: string;
  descriptionTemplate?: string | null;
  assignMode: AutomationUserSelector;
  targetUserId?: string | null;
  status?: string | null;
  priority?: string | null;
  dueDateOffsetDays?: number | null;
}

export interface SendNotificationParameters {
  recipientMode: AutomationUserSelector;
  targetUserId?: string | null;
  messageTemplate: string;
}

export interface AddFileTagParameters {
  tagName: string;
}

/** Available `{{...}}` template variables, listed in the builder for actions with a text template
 * (Add Comment / Create Task / Send Notification) — mirrors AutomationVariableResolver.cs. */
export const TEMPLATE_VARIABLES = [
  '{{task.title}}',
  '{{task.description}}',
  '{{task.id}}',
  '{{task.assignee}}',
  '{{task.project}}',
  '{{task.dueDate}}',
  '{{user.name}}',
];
