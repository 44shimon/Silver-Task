export interface UserSummary {
  id: string;
  name: string;
  email: string;
  /** So consumers like the assignee dropdown can exclude deactivated/deleted users from new
   * selections while still showing them wherever they're already referenced. */
  isActive: boolean;
}

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
  createdAt: string;
  updatedAt: string;
}

export interface ProjectMember {
  id: string;
  projectId: string;
  user: UserSummary;
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
