import { useMemo } from 'react';
import { useCurrentUser } from './useAuth';
import type { Project } from '@/types/project';

/** System-level permission check — "can(...)" reads the current user's own permission list
 * (populated on login/GET /auth/me, see CurrentUser.permissions). Centralizing this here means
 * UI code never needs `user.role === 'Administrator'` again; a single place decides what each
 * permission code means for the *currently logged-in* user's own account.
 *
 * This is a UX affordance only (hide/disable controls the user can't use) — the backend
 * re-checks every one of these independently on the actual request, per Phase 32's "frontend
 * hides it, backend also enforces it" rule (see e.g. RequireAdmin.tsx's own doc comment). */
export function usePermissions() {
  const { data: user } = useCurrentUser();

  return useMemo(() => {
    const permissions = new Set(user?.permissions ?? []);
    return {
      can: (permission: string) => permissions.has(permission),
    };
  }, [user]);
}

/** Project-scoped permission check — reads Project.myPermissions (populated by GET
 * /projects/{id}, computed server-side from the caller's ProjectMember.role for that specific
 * project). Returns false for every permission until `project` has loaded, so UI defaults to
 * "can't do this yet" rather than flashing editable controls before permissions are known. */
export function useProjectPermissions(project: Project | undefined) {
  return useMemo(() => {
    const permissions = new Set(project?.myPermissions ?? []);
    return {
      can: (permission: string) => permissions.has(permission),
    };
  }, [project]);
}
