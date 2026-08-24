import { useAdminUsers } from '@/hooks/useAdminUsers';
import { useCurrentUser } from '@/hooks/useAuth';
import { AdminUsersTable } from '@/components/admin/AdminUsersTable';
import { NewUserForm } from '@/components/admin/NewUserForm';
import './AdminUsersPage.css';

export function AdminUsersPage() {
  const { data: users, isLoading, isError } = useAdminUsers();
  const { data: currentUser } = useCurrentUser();

  return (
    <div className="admin-users-page">
      <div className="admin-users-page__toolbar">
        <NewUserForm />
      </div>

      {isLoading && <p>Loading users...</p>}
      {isError && <p>Users could not be loaded.</p>}

      {!isLoading && !isError && <AdminUsersTable users={users ?? []} currentUserId={currentUser?.id} />}
    </div>
  );
}
