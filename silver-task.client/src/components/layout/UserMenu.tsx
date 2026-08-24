import { useRef } from 'react';
import { Link } from 'react-router-dom';
import { ChevronDown, LogOut, Settings, User as UserIcon } from 'lucide-react';
import { useCurrentUser, useLogout } from '@/hooks/useAuth';
import './UserMenu.css';

// Same <details>-based popover pattern as every other toolbar dropdown in the app
// (Toolbar.css's .toolbar-popover), just anchored in the header instead of a grid toolbar.
export function UserMenu() {
  const { data: user } = useCurrentUser();
  const logout = useLogout();
  const detailsRef = useRef<HTMLDetailsElement>(null);

  if (!user) {
    return null;
  }

  function closeMenu() {
    if (detailsRef.current) {
      detailsRef.current.open = false;
    }
  }

  return (
    <details className="user-menu" ref={detailsRef}>
      <summary className="user-menu__trigger">
        <UserIcon size={16} />
        <span>{user.name}</span>
        <ChevronDown size={14} />
      </summary>
      <div className="user-menu__panel">
        <Link to="/settings" className="user-menu__item" onClick={closeMenu}>
          <UserIcon size={14} />
          <span>My Profile</span>
        </Link>
        <Link to="/settings/preferences" className="user-menu__item" onClick={closeMenu}>
          <Settings size={14} />
          <span>Settings</span>
        </Link>
        <button
          type="button"
          className="user-menu__item user-menu__item--button"
          onClick={() => logout.mutate()}
          disabled={logout.isPending}
        >
          <LogOut size={14} />
          <span>Logout</span>
        </button>
      </div>
    </details>
  );
}
