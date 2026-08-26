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

/** The full type union now lives in @/types/notification (Phase 28) — the notification
 * *preference* rows here just carry whichever type string the backend returns. */
export interface NotificationSetting {
  notificationType: string;
  isEnabled: boolean;
}

/** Mirrors Common/SystemSettingDefinitions.cs's SystemSettingSection enum server-side. */
export type SystemSettingSection = 'General' | 'TaskDefaults' | 'ProjectDefaults' | 'Security' | 'Behavior' | 'Attachments';

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
