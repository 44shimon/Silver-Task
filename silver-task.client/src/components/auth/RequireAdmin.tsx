import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useCurrentUser } from '@/hooks/useAuth';
import { usePermissions } from '@/hooks/usePermissions';
import { Permissions } from '@/types/permissions';

interface RequireAdminProps {
  children: ReactNode;
}

/** Nested inside RequireAuth (so a logged-out user hits the login redirect first), this only
 * adds the permission check (Phase 32: Administration.Access, which today only Administrator
 * grants — see PermissionService.SystemMatrix). The backend enforces the same rule independently
 * on every admin endpoint ([Authorize(Roles = Administrator)]) — this is a UX guard, not the
 * security boundary, exactly per the "frontend hides it, backend also enforces it" requirement. */
export function RequireAdmin({ children }: RequireAdminProps) {
  const { isLoading } = useCurrentUser();
  const { can } = usePermissions();

  if (isLoading) {
    return <div className="auth-loading">Loading...</div>;
  }

  if (!can(Permissions.AdministrationAccess)) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
