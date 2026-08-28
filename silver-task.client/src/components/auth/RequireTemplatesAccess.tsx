import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useCurrentUser } from '@/hooks/useAuth';
import { usePermissions } from '@/hooks/usePermissions';
import { Permissions } from '@/types/permissions';

interface RequireTemplatesAccessProps {
  children: ReactNode;
}

/** Nested inside RequireAuth, mirrors RequireReportsAccess exactly — a UX guard only (frontend
 * hides the nav item/route, backend independently re-checks Permissions.TemplatesView on every
 * /api/templates, /api/project-templates, /api/task-templates endpoint). */
export function RequireTemplatesAccess({ children }: RequireTemplatesAccessProps) {
  const { isLoading } = useCurrentUser();
  const { can } = usePermissions();

  if (isLoading) {
    return <div className="auth-loading">Loading...</div>;
  }

  if (!can(Permissions.TemplatesView)) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
