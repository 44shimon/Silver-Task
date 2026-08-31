export type Theme = 'Light' | 'Dark' | 'System';
export type TimeFormat = '12h' | '24h';
export type DefaultLandingPage = 'Dashboard' | 'MyTasks' | 'LastVisited';
/** Mirrors Common/NotificationDeliveryModes.cs. */
export type EmailDeliveryMode = 'Immediately' | 'DailyDigest' | 'WeeklyDigest' | 'Off';
export const EMAIL_DELIVERY_MODE_LABELS: Record<EmailDeliveryMode, string> = {
  Immediately: 'Immediately',
  DailyDigest: 'Daily Digest',
  WeeklyDigest: 'Weekly Digest',
  Off: 'Off',
};

export interface UserPreferences {
  theme: Theme;
  defaultProjectId: string | null;
  /** One of the five project view ids, or null for "no preference — use Table". */
  defaultTaskView: string | null;
  dateFormat: string;
  timeFormat: TimeFormat;
  timeZone: string;
  itemsPerPage: number;
  /** Phase 45 — master switch, checked before any per-type email preference (see
   * UserPreference.EmailNotificationsEnabled server-side). */
  emailNotificationsEnabled: boolean;
  quietHoursEnabled: boolean;
  /** "HH:mm:ss" (TimeOnly on the wire) — only meaningful when quietHoursEnabled is true. */
  quietHoursStart: string | null;
  quietHoursEnd: string | null;
  /** Phase 46 — "HH:mm:ss" (TimeOnly), interpreted in timeZone above. */
  dailyDigestTime: string;
  /** A System.DayOfWeek name, e.g. "Monday". */
  weeklyDigestDay: string;
  weeklyDigestTime: string;
  defaultLandingPage: DefaultLandingPage;
  /** Raw JSON string (DashboardLayout) — parsed/shaped entirely client-side, see
   * @/types/dashboard's DashboardLayout interface. Null means "no customization saved yet". */
  dashboardLayout: string | null;
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

/** The full type union now lives in @/types/notification (Phase 28) — the notification
 * *preference* rows here just carry whichever type string the backend returns. In-app and
 * email are independently controllable per type (Phase 36). */
export interface NotificationSetting {
  notificationType: string;
  inAppEnabled: boolean;
  emailDeliveryMode: EmailDeliveryMode;
  /** True for Urgent-priority types (currently only TaskOverdue) — the server always overrides
   * these to Immediately regardless of what's stored/posted; the UI disables the dropdown and
   * shows why rather than letting the user pick a mode that silently has no effect. */
  alwaysImmediate: boolean;
}

/** Mirrors Common/SystemSettingDefinitions.cs's SystemSettingSection enum server-side. */
export type SystemSettingSection =
  | 'General'
  | 'TaskDefaults'
  | 'ProjectDefaults'
  | 'Security'
  | 'Behavior'
  | 'Attachments'
  | 'Notifications';

/** Every value is a string on the wire (the EAV Key/Value store) — ValueType tells the UI how
 * to render/parse it ("bool" | "int" | "string"), but the server is still the sole source of
 * truth for validation; nothing client-side is trusted as the real check. */
export interface SystemSetting {
  key: string;
  section: SystemSettingSection;
  value: string;
  valueType: 'bool' | 'int' | 'string';
  description: string | null;
  updatedAt: string | null;
  updatedByName: string | null;
}

export interface PublicSettings {
  applicationName: string;
  applicationDescription: string;
}
