import { NavLink, Outlet } from 'react-router-dom';
import './AdminLayout.css';

// "Custom Fields" is still visibly-present-but-disabled (same convention as ProjectViewTabs for
// not-yet-built sections) — "System Settings" moved to a real tab in Phase 24.
const DISABLED_SECTIONS = ['Custom Fields'];

export function AdminLayout() {
  return (
    <div className="admin-layout">
      <div className="admin-layout__header">
        <h1>Admin</h1>
      </div>

      <nav className="admin-layout__nav" role="tablist">
        <NavLink
          to="/admin"
          end
          className={({ isActive }) => `admin-layout__nav-item${isActive ? ' admin-layout__nav-item--active' : ''}`}
        >
          Dashboard
        </NavLink>
        <NavLink
          to="/admin/users"
          className={({ isActive }) => `admin-layout__nav-item${isActive ? ' admin-layout__nav-item--active' : ''}`}
        >
          Users
        </NavLink>
        <NavLink
          to="/admin/projects"
          className={({ isActive }) => `admin-layout__nav-item${isActive ? ' admin-layout__nav-item--active' : ''}`}
        >
          Projects
        </NavLink>
        <NavLink
          to="/admin/settings"
          className={({ isActive }) => `admin-layout__nav-item${isActive ? ' admin-layout__nav-item--active' : ''}`}
        >
          System Settings
        </NavLink>
        {DISABLED_SECTIONS.map((section) => (
          <button key={section} type="button" className="admin-layout__nav-item" disabled title="Coming soon">
            {section}
          </button>
        ))}
      </nav>

      <div className="admin-layout__content">
        <Outlet />
      </div>
    </div>
  );
}
