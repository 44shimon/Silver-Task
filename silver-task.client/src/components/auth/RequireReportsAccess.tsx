import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useCurrentUser } from '@/hooks/useAuth';
import { usePermissions } from '@/hooks/usePermissions';
import { Permissions } from '@/types/permissions';

interface RequireReportsAccessProps {
  children: ReactNode;
}

/** Nested inside RequireAuth, mirrors RequireAdmin exactly — a UX guard only (frontend hides the
 * nav item/route, backend independently re-checks Permissions.ReportsView on every
 * /api/reports/* endpoint via ReportsController.EnsureCanViewReportsAsync). */
export function RequireReportsAccess({ children }: RequireReportsAccessProps) {
  const { isLoading } = useCurrentUser();
  const { can } = usePermissions();

  if (isLoading) {
    return <div className="auth-loading">Loading...</div>;
  }

  if (!can(Permissions.ReportsView)) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
