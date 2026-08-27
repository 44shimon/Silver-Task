import type { UserSummary } from './project';

/** The set of types is driven entirely by the backend (Common.NotificationTypes) — this union
 * exists only for the display-label/icon lookups in the UI, not as a source of truth. */
export type NotificationType =
  | 'TaskAssigned'
  | 'TaskReassigned'
  | 'TaskUnassigned'
  | 'TaskStatusChanged'
  | 'TaskPriorityChanged'
  | 'TaskDueDateChanged'
  | 'TaskDueSoon'
  | 'TaskOverdue'
  | 'TaskCompleted'
  | 'TaskReopened'
  | 'CommentAdded'
  | 'MentionedInComment'
  | 'UserAddedToProject'
  | 'UserRemovedFromProject'
  | 'ProjectTaskCompleted'
  | 'ProjectStatusChanged'
  | 'ProjectRoleChanged'
  | 'SystemRoleChanged'
  | 'TaskDependencyCompleted'
  | 'RecurringTaskAssigneeInactive'
  | 'FileUploaded'
  | 'AutomationNotification';

/** Mirrors Models/Entities/Enums/NotificationPriority.cs. */
export type NotificationPriority = 'Normal' | 'Important' | 'Urgent';

/** The notification center's own tab groupings — coarser than NotificationType (several types
 * fold into one tab, e.g. every Task* type is one "Tasks" tab), computed client-side from a
 * notification's `type` string rather than a separate backend concept. */
export type NotificationCategory = 'all' | 'unread' | 'mentions' | 'tasks' | 'projects' | 'files' | 'automations' | 'system';

const TASK_TYPES = new Set<string>([
  'TaskAssigned', 'TaskReassigned', 'TaskUnassigned', 'TaskStatusChanged', 'TaskPriorityChanged',
  'TaskDueDateChanged', 'TaskDueSoon', 'TaskOverdue', 'TaskCompleted', 'TaskReopened',
  'CommentAdded', 'ProjectTaskCompleted', 'TaskDependencyCompleted', 'RecurringTaskAssigneeInactive',
]);
const PROJECT_TYPES = new Set<string>(['UserAddedToProject', 'UserRemovedFromProject', 'ProjectStatusChanged', 'ProjectRoleChanged']);
const SYSTEM_TYPES = new Set<string>(['SystemRoleChanged']);

export function categoryOf(type: string): NotificationCategory {
  if (type === 'MentionedInComment') return 'mentions';
  if (type === 'FileUploaded') return 'files';
  if (type === 'AutomationNotification') return 'automations';
  if (SYSTEM_TYPES.has(type)) return 'system';
  if (PROJECT_TYPES.has(type)) return 'projects';
  if (TASK_TYPES.has(type)) return 'tasks';
  return 'system';
}

export interface AppNotification {
  id: string;
  type: string;
  title: string;
  message: string;
  priority: NotificationPriority;
  /** Null for system/background-sweep-originated notifications with no human actor. */
  actor: UserSummary | null;
  /** Present only while the source task still exists — null means "open task" isn't available
   * anymore, not that this notification never had one. */
  taskId: string | null;
  projectId: string | null;
  commentId: string | null;
  fileId: string | null;
  /** Precomputed deep link path (e.g. "/projects/{id}?task={id}") — the destination route always
   * re-enforces its own authorization on load, so a stale/inaccessible link degrades to that
   * route's normal 403/404, never a way to see something the caller no longer has access to. */
  actionUrl: string | null;
  isRead: boolean;
  readAt: string | null;
  createdAt: string;
}

export interface NotificationList {
  items: AppNotification[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface NotificationFilter {
  isRead?: boolean;
  page?: number;
  pageSize?: number;
  search?: string;
  type?: string;
  /** Server-resolved multi-type group (Tasks/Projects/Mentions/Files/Automations/System) — see
   * Common/NotificationCategories.cs. "all"/"unread" aren't sent as a category; "unread" maps to
   * isRead=false instead. */
  category?: Exclude<NotificationCategory, 'all' | 'unread'>;
  priority?: NotificationPriority;
  projectId?: string;
  taskId?: string;
  dateFrom?: string;
  dateTo?: string;
}
