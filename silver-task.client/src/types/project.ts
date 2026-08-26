export interface UserSummary {
  id: string;
  name: string;
  email: string;
  /** So consumers like the assignee dropdown can exclude deactivated/deleted users from new
   * selections while still showing them wherever they're already referenced. */
  isActive: boolean;
}

/** Per-project role (Phase 32) — independent of the member's system-wide UserRole. See
 * ProjectMember.role. */
export type ProjectRole = 'Manager' | 'Member' | 'Viewer';

export interface Project {
  id: string;
  name: string;
  description: string | null;
  owner: UserSummary;
  memberCount: number;
  /** Only populated by the endpoints that bother computing it (the Admin Projects list) — null elsewhere. */
  taskCount: number | null;
  isArchived: boolean;
  archivedAt: string | null;
  /** The caller's own effective permission codes within this project (Phase 32) — only
   * populated by GET /projects/{id} (the single-project fetch every ProjectPage load already
   * makes); null on list endpoints. See useProjectPermissions(). */
  myPermissions: string[] | null;
  createdAt: string;
  updatedAt: string;
}

export interface ProjectMember {
  id: string;
  projectId: string;
  user: UserSummary;
  role: ProjectRole;
  createdAt: string;
}

export interface CreateProjectRequest {
  name: string;
  description?: string;
}

export interface UpdateProjectRequest {
  name: string;
  description?: string;
}

export interface AddProjectMemberRequest {
  email: string;
}
