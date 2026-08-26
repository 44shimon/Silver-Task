import { Check } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useAdminProjectRoles, useAdminSystemRoles } from '@/hooks/useAdminRoles';
import type { RoleInfo } from '@/types/admin';
import { PERMISSION_GROUPS, PERMISSION_LABELS } from '@/types/permissions';
import './AdminRolesPage.css';

/** Admin -> Roles & Permissions (Phase 32) — a read-only viewer for the fixed role/permission
 * matrix (system roles: Administrator/Manager/Member/Viewer; project roles: Manager/Member/
 * Viewer). There is deliberately no editor here: the matrix itself (which permissions exist,
 * which roles grant them) is fixed, code-defined configuration on the backend
 * (PermissionService) — what's admin-editable is which role a *user* or *project membership*
 * has, via the Admin Users page and each project's Members section, not what a role means. */
export function AdminRolesPage() {
  const { data: systemRoles, isLoading: systemLoading } = useAdminSystemRoles();
  const { data: projectRoles, isLoading: projectLoading } = useAdminProjectRoles();

  return (
    <div className="admin-roles-page">
      <section>
        <h2 className="admin-roles-page__section-title">System Roles</h2>
        <p className="admin-roles-page__hint">
          Assigned per user on the <Link to="/admin/users">Users</Link> page.
        </p>
        {systemLoading && <p>Loading...</p>}
        <div className="admin-roles-page__grid">
          {systemRoles?.map((role) => <RoleCard key={role.name} role={role} />)}
        </div>
      </section>

      <section>
        <h2 className="admin-roles-page__section-title">Project Roles</h2>
        <p className="admin-roles-page__hint">Assigned per member, inside each project's Members section.</p>
        {projectLoading && <p>Loading...</p>}
        <div className="admin-roles-page__grid">
          {projectRoles?.map((role) => <RoleCard key={role.name} role={role} />)}
        </div>
      </section>
    </div>
  );
}

function RoleCard({ role }: { role: RoleInfo }) {
  const granted = new Set(role.permissions);

  return (
    <div className="admin-roles-page__card">
      <div className="admin-roles-page__card-header">
        <h3>{role.name}</h3>
        <span className="admin-roles-page__user-count">
          {role.userCount} {role.userCount === 1 ? 'assignment' : 'assignments'}
        </span>
      </div>

      {Object.entries(PERMISSION_GROUPS).map(([group, codes]) => (
        <div className="admin-roles-page__group" key={group}>
          <span className="admin-roles-page__group-title">{group}</span>
          {codes.map((code) => (
            <label className="admin-roles-page__permission" key={code}>
              <span className={`admin-roles-page__checkbox${granted.has(code) ? ' admin-roles-page__checkbox--checked' : ''}`}>
                {granted.has(code) && <Check size={11} />}
              </span>
              {PERMISSION_LABELS[code]}
            </label>
          ))}
        </div>
      ))}
    </div>
  );
}
