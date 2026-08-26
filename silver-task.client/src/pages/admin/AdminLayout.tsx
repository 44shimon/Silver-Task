import { NavLink, Outlet } from 'react-router-dom';
import './AdminLayout.css';

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
          to="/admin/roles"
          className={({ isActive }) => `admin-layout__nav-item${isActive ? ' admin-layout__nav-item--active' : ''}`}
        >
          Roles &amp; Permissions
        </NavLink>
        <NavLink
          to="/admin/projects"
          className={({ isActive }) => `admin-layout__nav-item${isActive ? ' admin-layout__nav-item--active' : ''}`}
        >
          Projects
        </NavLink>
        <NavLink
          to="/admin/custom-fields"
          className={({ isActive }) => `admin-layout__nav-item${isActive ? ' admin-layout__nav-item--active' : ''}`}
        >
          Custom Fields
        </NavLink>
        <NavLink
          to="/admin/tags"
          className={({ isActive }) => `admin-layout__nav-item${isActive ? ' admin-layout__nav-item--active' : ''}`}
        >
          Tags
        </NavLink>
        <NavLink
          to="/admin/file-categories"
          className={({ isActive }) => `admin-layout__nav-item${isActive ? ' admin-layout__nav-item--active' : ''}`}
        >
          File Categories
        </NavLink>
        <NavLink
          to="/admin/settings"
          className={({ isActive }) => `admin-layout__nav-item${isActive ? ' admin-layout__nav-item--active' : ''}`}
        >
          System Settings
        </NavLink>
      </nav>

      <div className="admin-layout__content">
        <Outlet />
      </div>
    </div>
  );
}
