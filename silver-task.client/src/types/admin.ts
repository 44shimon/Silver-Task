import type { CurrentUser, UserRole } from './auth';

/** Same shape the API returns for the logged-in user (UserDto on the backend) — reused here
 * rather than declaring a parallel type, since an admin-managed user and "the current user"
 * are the same resource. */
export type AdminUser = CurrentUser;

export interface CreateUserRequest {
  name: string;
  email: string;
  password: string;
  role: UserRole;
}

export interface UpdateUserRequest {
  name: string;
  role: UserRole;
  isActive: boolean;
}

export interface ResetPasswordRequest {
  newPassword: string;
}

export interface AdminStats {
  totalUsers: number;
  activeUsers: number;
  totalProjects: number;
  totalTasks: number;
  openTasks: number;
  completedTasks: number;
}

/** One row of the read-only Admin -> Roles & Permissions matrix (Phase 32) — a role's fixed
 * permission set plus how many users/memberships currently have it. See PermissionService's own
 * doc comment (backend) for why this is fixed/code-defined rather than admin-editable. */
export interface RoleInfo {
  name: string;
  permissions: string[];
  userCount: number;
}

/** Admin-only storage connectivity probe (Phase 33) — deliberately has no path/credential
 * fields; the server never sends its raw filesystem path to the client. */
export interface StorageHealth {
  isWritable: boolean;
  provider: string;
  fileCount: number;
  totalBytes: number;
}

/** Shown in the delete-user confirmation dialog before an admin can commit to deleting a
 * user — deletion is a soft delete, so nothing here is actually destroyed, but the admin should
 * still see what historical data stays attached to the now-deleted account. */
export interface UserDeletionImpact {
  name: string;
  email: string;
  role: string;
  assignedTaskCount: number;
  projectCount: number;
  commentCount: number;
  activityCount: number;
}
