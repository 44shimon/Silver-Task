import { Bell, LogOut, Search, User } from 'lucide-react';
import { useCurrentUser, useLogout } from '@/hooks/useAuth';

export function Topbar() {
  const { data: user } = useCurrentUser();
  const logout = useLogout();

  return (
    <header className="topbar">
      <div className="topbar__brand">Silver-Task</div>
      <div className="topbar__search">
        <Search size={16} />
        <input type="text" placeholder="Search tasks..." disabled />
      </div>
      <div className="topbar__actions">
        <button className="icon-button" type="button" aria-label="Notifications" disabled>
          <Bell size={18} />
        </button>
        {user && (
          <div className="topbar__user">
            <User size={16} />
            <span>{user.name}</span>
          </div>
        )}
        <button
          className="icon-button"
          type="button"
          aria-label="Log out"
          onClick={() => logout.mutate()}
          disabled={logout.isPending}
        >
          <LogOut size={18} />
        </button>
      </div>
    </header>
  );
}
