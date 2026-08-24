import { NavLink, Outlet } from 'react-router-dom';
import './AdminLayout.css';

// "Custom Fields" and "System Settings" are visibly-present-but-disabled, same convention as
// ProjectViewTabs for not-yet-built sections — Users and Projects are the two this phase builds.
const DISABLED_SECTIONS = ['Custom Fields', 'System Settings'];

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
