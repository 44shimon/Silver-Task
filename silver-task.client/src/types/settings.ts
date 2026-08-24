export type Theme = 'Light' | 'Dark' | 'System';
export type TimeFormat = '12h' | '24h';

export interface UserPreferences {
  theme: Theme;
  defaultProjectId: string | null;
  /** One of the five project view ids, or null for "no preference — use Table". */
  defaultTaskView: string | null;
  dateFormat: string;
  timeFormat: TimeFormat;
  timeZone: string;
  itemsPerPage: number;
}

export type UpdatePreferencesRequest = UserPreferences;

export interface UpdateProfileRequest {
  name: string;
  email: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}

/** The set of types is driven entirely by what the backend returns (NotificationTypes.All) —
 * this union exists only for the display-label lookup below, not as a source of truth. */
export type NotificationType =
  | 'TaskAssigned'
  | 'TaskStatusChanged'
  | 'TaskDueSoon'
  | 'TaskOverdue'
  | 'CommentAdded'
  | 'MentionedInComment';

export interface NotificationSetting {
  notificationType: string;
  isEnabled: boolean;
}
