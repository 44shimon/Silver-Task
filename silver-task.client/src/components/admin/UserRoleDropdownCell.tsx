import type { ChangeEvent } from 'react';
import { ChevronDown } from 'lucide-react';
import type { AdminUser } from '@/types/admin';
import type { UserRole } from '@/types/auth';
import { buildUserUpdateRequest, useUpdateUser } from '@/hooks/useAdminUsers';
import '@/components/spreadsheet/DropdownCell.css';
import './AdminUsersTable.css';

const ROLE_OPTIONS: UserRole[] = ['Administrator', 'Manager', 'Member', 'Viewer'];

interface UserRoleDropdownCellProps {
  user: AdminUser;
  /** True only for "yourself, currently an Administrator" — the backend rejects removing
   * your own Administrator role (self-lockout), so the picker is disabled to match. */
  disabled?: boolean;
}

export function UserRoleDropdownCell({ user, disabled }: UserRoleDropdownCellProps) {
  const updateUser = useUpdateUser();

  function handleChange(event: ChangeEvent<HTMLSelectElement>) {
    const role = event.target.value as UserRole;
    if (role !== user.role) {
      updateUser.mutate({ id: user.id, request: buildUserUpdateRequest(user, { role }) });
    }
  }

  return (
    <div className="dropdown-cell-wrapper">
      <select
        className={`dropdown-cell dropdown-cell--badge admin-role-badge--${user.role.toLowerCase()}${updateUser.isError ? ' dropdown-cell--error' : ''}`}
        value={user.role}
        onChange={handleChange}
        disabled={disabled || updateUser.isPending}
        title={
          disabled
            ? "You can't remove your own Administrator role"
            : updateUser.isError
              ? 'Could not save — try again'
              : undefined
        }
      >
        {ROLE_OPTIONS.map((role) => (
          <option key={role} value={role}>
            {role}
          </option>
        ))}
      </select>
      <ChevronDown size={12} className="dropdown-cell__chevron" />
    </div>
  );
}
