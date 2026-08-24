import type { AdminUser } from '@/types/admin';
import { buildUserUpdateRequest, useUpdateUser } from '@/hooks/useAdminUsers';
import './AdminUsersTable.css';

interface UserActiveToggleCellProps {
  user: AdminUser;
  /** True only for "yourself" — the backend rejects disabling your own account. */
  disabled?: boolean;
}

export function UserActiveToggleCell({ user, disabled }: UserActiveToggleCellProps) {
  const updateUser = useUpdateUser();

  function toggle() {
    updateUser.mutate({ id: user.id, request: buildUserUpdateRequest(user, { isActive: !user.isActive }) });
  }

  return (
    <button
      type="button"
      className={`admin-active-toggle admin-active-toggle--${user.isActive ? 'active' : 'inactive'}`}
      onClick={toggle}
      disabled={disabled || updateUser.isPending}
      title={
        disabled
          ? "You can't disable your own account"
          : user.isActive
            ? 'Click to disable'
            : 'Click to enable'
      }
    >
      {user.isActive ? 'Active' : 'Inactive'}
    </button>
  );
}
