export type UserRole = 'Administrator' | 'Manager' | 'Member' | 'Viewer';

export interface CurrentUser {
  id: string;
  name: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  /** The caller's own system-level permission codes (Phase 32, e.g. "Projects.Create",
   * "Administration.Access"). Populated on /auth/login and /auth/me; null when this same
   * UserDto/CurrentUser shape is reused for a *different* user (e.g. AdminUser rows in the
   * admin users list), which has no reason to compute another user's permission set. See
   * usePermissions(). */
  permissions: string[] | null;
  createdAt: string;
  updatedAt: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}
