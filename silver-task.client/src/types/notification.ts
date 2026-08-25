/** The set of types is driven entirely by the backend (Common.NotificationTypes) — this union
 * exists only for the display-label/icon lookups in the UI, not as a source of truth. */
export type NotificationType =
  | 'TaskAssigned'
  | 'TaskReassigned'
  | 'TaskStatusChanged'
  | 'TaskPriorityChanged'
  | 'TaskDueDateChanged'
  | 'TaskDueSoon'
  | 'TaskOverdue'
  | 'CommentAdded'
  | 'MentionedInComment'
  | 'UserAddedToProject'
  | 'UserRemovedFromProject'
  | 'ProjectTaskCompleted'
  | 'TaskDependencyCompleted';

export interface AppNotification {
  id: string;
  type: string;
  title: string;
  message: string;
  /** Present only while the source task still exists — null means "open task" isn't available
   * anymore, not that this notification never had one. */
  taskId: string | null;
  projectId: string | null;
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
}
