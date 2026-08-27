import { NavLink, Outlet } from 'react-router-dom';
import './SettingsLayout.css';

// Same shell as AdminLayout (header + nav tabs + <Outlet/>) — nested routes rather than
// in-page tab state, so each section is linkable/bookmarkable, matching the app's existing
// convention for multi-section admin-style pages.
export function SettingsLayout() {
  return (
    <div className="settings-layout">
      <div className="settings-layout__header">
        <h1>Settings</h1>
      </div>

      <nav className="settings-layout__nav" role="tablist">
        <NavLink
          to="/settings"
          end
          className={({ isActive }) => `settings-layout__nav-item${isActive ? ' settings-layout__nav-item--active' : ''}`}
        >
          Profile
        </NavLink>
        <NavLink
          to="/settings/preferences"
          className={({ isActive }) => `settings-layout__nav-item${isActive ? ' settings-layout__nav-item--active' : ''}`}
        >
          Preferences
        </NavLink>
        <NavLink
          to="/settings/notifications"
          className={({ isActive }) => `settings-layout__nav-item${isActive ? ' settings-layout__nav-item--active' : ''}`}
        >
          Notifications
        </NavLink>
        <NavLink
          to="/settings/dashboard"
          className={({ isActive }) => `settings-layout__nav-item${isActive ? ' settings-layout__nav-item--active' : ''}`}
        >
          Dashboard
        </NavLink>
        <NavLink
          to="/settings/security"
          className={({ isActive }) => `settings-layout__nav-item${isActive ? ' settings-layout__nav-item--active' : ''}`}
        >
          Security
        </NavLink>
      </nav>

      <div className="settings-layout__content">
        <Outlet />
      </div>
    </div>
  );
}
