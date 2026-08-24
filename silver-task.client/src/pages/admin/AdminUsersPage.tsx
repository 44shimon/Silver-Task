import { useState } from 'react';
import { useAdminUsers } from '@/hooks/useAdminUsers';
import { useCurrentUser } from '@/hooks/useAuth';
import type { AdminUser } from '@/types/admin';
import { AdminUsersTable } from '@/components/admin/AdminUsersTable';
import { NewUserForm } from '@/components/admin/NewUserForm';
import { ResetPasswordDialog } from '@/components/admin/ResetPasswordDialog';
import './AdminUsersPage.css';

export function AdminUsersPage() {
  const { data: users, isLoading, isError } = useAdminUsers();
  const { data: currentUser } = useCurrentUser();
  const [resetPasswordUser, setResetPasswordUser] = useState<AdminUser | null>(null);

  return (
    <div className="admin-users-page">
      <div className="admin-users-page__toolbar">
        <NewUserForm />
      </div>

      {isLoading && <p>Loading users...</p>}
      {isError && <p>Users could not be loaded.</p>}

      {!isLoading && !isError && (
        <AdminUsersTable users={users ?? []} currentUserId={currentUser?.id} onResetPassword={setResetPasswordUser} />
      )}

      {resetPasswordUser && (
        <ResetPasswordDialog user={resetPasswordUser} onClose={() => setResetPasswordUser(null)} />
      )}
    </div>
  );
}
