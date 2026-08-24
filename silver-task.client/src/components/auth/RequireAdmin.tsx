import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useCurrentUser } from '@/hooks/useAuth';

interface RequireAdminProps {
  children: ReactNode;
}

/** Nested inside RequireAuth (so a logged-out user hits the login redirect first), this only
 * adds the role check. The backend enforces the same rule independently on every admin
 * endpoint ([Authorize(Roles = Administrator)]) — this is a UX guard, not the security
 * boundary, exactly per the "frontend hides it, backend also enforces it" requirement. */
export function RequireAdmin({ children }: RequireAdminProps) {
  const { data: user, isLoading } = useCurrentUser();

  if (isLoading) {
    return <div className="auth-loading">Loading...</div>;
  }

  if (user?.role !== 'Administrator') {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
