import { Route, Routes } from 'react-router-dom';
import { AppShell } from '@/components/layout/AppShell';
import { RequireAuth } from '@/components/auth/RequireAuth';
import { RequireAdmin } from '@/components/auth/RequireAdmin';
import { MyTasksPage } from '@/pages/MyTasksPage';
import { NotificationsPage } from '@/pages/NotificationsPage';
import { LoginPage } from '@/pages/LoginPage';
import { ProjectPage } from '@/pages/ProjectPage';
import { AdminLayout } from '@/pages/admin/AdminLayout';
import { AdminDashboardPage } from '@/pages/admin/AdminDashboardPage';
import { AdminUsersPage } from '@/pages/admin/AdminUsersPage';
import { AdminRolesPage } from '@/pages/admin/AdminRolesPage';
import { AdminProjectsPage } from '@/pages/admin/AdminProjectsPage';
import { AdminSystemSettingsPage } from '@/pages/admin/AdminSystemSettingsPage';
import { AdminCustomFieldsPage } from '@/pages/admin/AdminCustomFieldsPage';
import { SettingsLayout } from '@/pages/settings/SettingsLayout';
import { ProfileSettingsPage } from '@/pages/settings/ProfileSettingsPage';
import { PreferencesSettingsPage } from '@/pages/settings/PreferencesSettingsPage';
import { NotificationSettingsPage } from '@/pages/settings/NotificationSettingsPage';
import { SecuritySettingsPage } from '@/pages/settings/SecuritySettingsPage';

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/*"
        element={
          <RequireAuth>
            <AppShell>
              <Routes>
                <Route path="/" element={<MyTasksPage />} />
                <Route path="/notifications" element={<NotificationsPage />} />
                <Route path="/projects/:projectId" element={<ProjectPage />} />
                <Route path="/settings" element={<SettingsLayout />}>
                  <Route index element={<ProfileSettingsPage />} />
                  <Route path="preferences" element={<PreferencesSettingsPage />} />
                  <Route path="notifications" element={<NotificationSettingsPage />} />
                  <Route path="security" element={<SecuritySettingsPage />} />
                </Route>
                <Route
                  path="/admin"
                  element={
                    <RequireAdmin>
                      <AdminLayout />
                    </RequireAdmin>
                  }
                >
                  <Route index element={<AdminDashboardPage />} />
                  <Route path="users" element={<AdminUsersPage />} />
                  <Route path="roles" element={<AdminRolesPage />} />
                  <Route path="projects" element={<AdminProjectsPage />} />
                  <Route path="custom-fields" element={<AdminCustomFieldsPage />} />
                  <Route path="settings" element={<AdminSystemSettingsPage />} />
                </Route>
              </Routes>
            </AppShell>
          </RequireAuth>
        }
      />
    </Routes>
  );
}
