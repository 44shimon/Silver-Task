export type Theme = 'Light' | 'Dark' | 'System';
export type TimeFormat = '12h' | '24h';
export type DigestFrequency = 'Immediately' | 'Daily' | 'Never';
export type DefaultLandingPage = 'Dashboard' | 'MyTasks' | 'LastVisited';

export interface UserPreferences {
  theme: Theme;
  defaultProjectId: string | null;
  /** One of the five project view ids, or null for "no preference — use Table". */
  defaultTaskView: string | null;
  dateFormat: string;
  timeFormat: TimeFormat;
  timeZone: string;
  itemsPerPage: number;
  /** Immediately (default): each eligible email sends as it happens. Daily: batched into one
   * digest email (Urgent notifications, e.g. overdue, still send immediately regardless). Never:
   * no notification email at all. Purely an email-channel setting — in-app is unaffected. */
  digestFrequency: DigestFrequency;
  quietHoursEnabled: boolean;
  /** "HH:mm:ss" (TimeOnly on the wire) — only meaningful when quietHoursEnabled is true. */
  quietHoursStart: string | null;
  quietHoursEnd: string | null;
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
  emailEnabled: boolean;
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
