import { Route, Routes } from 'react-router-dom';
import { AppShell } from '@/components/layout/AppShell';
import { RequireAuth } from '@/components/auth/RequireAuth';
import { RequireAdmin } from '@/components/auth/RequireAdmin';
import { RequireReportsAccess } from '@/components/auth/RequireReportsAccess';
import { RequireTemplatesAccess } from '@/components/auth/RequireTemplatesAccess';
import { MyTasksPage } from '@/pages/MyTasksPage';
import { DashboardPage } from '@/pages/DashboardPage';
import { LandingRedirect } from '@/pages/LandingRedirect';
import { NotificationsPage } from '@/pages/NotificationsPage';
import { LoginPage } from '@/pages/LoginPage';
import { ProjectPage } from '@/pages/ProjectPage';
import { FavoriteFilesPage } from '@/pages/files/FavoriteFilesPage';
import { RecentFilesPage } from '@/pages/files/RecentFilesPage';
import { AdminLayout } from '@/pages/admin/AdminLayout';
import { AdminDashboardPage } from '@/pages/admin/AdminDashboardPage';
import { AdminUsersPage } from '@/pages/admin/AdminUsersPage';
import { AdminRolesPage } from '@/pages/admin/AdminRolesPage';
import { AdminProjectsPage } from '@/pages/admin/AdminProjectsPage';
import { AdminSystemSettingsPage } from '@/pages/admin/AdminSystemSettingsPage';
import { AdminEmailSettingsPage } from '@/pages/admin/AdminEmailSettingsPage';
import { AdminCustomFieldsPage } from '@/pages/admin/AdminCustomFieldsPage';
import { AdminTagsPage } from '@/pages/admin/AdminTagsPage';
import { AdminFileCategoriesPage } from '@/pages/admin/AdminFileCategoriesPage';
import { AdminAutomationsPage } from '@/pages/admin/AdminAutomationsPage';
import { SettingsLayout } from '@/pages/settings/SettingsLayout';
import { ProfileSettingsPage } from '@/pages/settings/ProfileSettingsPage';
import { PreferencesSettingsPage } from '@/pages/settings/PreferencesSettingsPage';
import { NotificationSettingsPage } from '@/pages/settings/NotificationSettingsPage';
import { SecuritySettingsPage } from '@/pages/settings/SecuritySettingsPage';
import { DashboardSettingsPage } from '@/pages/settings/DashboardSettingsPage';
import { ReportsPage } from '@/pages/ReportsPage';
import { TemplatesPage } from '@/pages/TemplatesPage';
import { ProjectTemplateBuilderPage } from '@/pages/templates/ProjectTemplateBuilderPage';
import { TaskTemplateBuilderPage } from '@/pages/templates/TaskTemplateBuilderPage';
import { CreateProjectFromTemplateWizardPage } from '@/pages/templates/CreateProjectFromTemplateWizardPage';
import { SearchResultsPage } from '@/pages/SearchResultsPage';
import { ViewsPage } from '@/pages/ViewsPage';
import { SavedViewPage } from '@/pages/SavedViewPage';

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
                <Route path="/" element={<LandingRedirect />} />
                <Route path="/dashboard" element={<DashboardPage />} />
                <Route path="/my-tasks" element={<MyTasksPage />} />
                <Route path="/notifications" element={<NotificationsPage />} />
                <Route path="/search" element={<SearchResultsPage />} />
                <Route path="/views" element={<ViewsPage />} />
                <Route path="/views/:id" element={<SavedViewPage />} />
                <Route path="/projects/:projectId" element={<ProjectPage />} />
                <Route path="/files/favorites" element={<FavoriteFilesPage />} />
                <Route path="/files/recent" element={<RecentFilesPage />} />
                <Route
                  path="/reports"
                  element={
                    <RequireReportsAccess>
                      <ReportsPage />
                    </RequireReportsAccess>
                  }
                />
                <Route
                  path="/reports/:type"
                  element={
                    <RequireReportsAccess>
                      <ReportsPage />
                    </RequireReportsAccess>
                  }
                />
                <Route
                  path="/templates"
                  element={
                    <RequireTemplatesAccess>
                      <TemplatesPage />
                    </RequireTemplatesAccess>
                  }
                />
                <Route
                  path="/templates/new-project"
                  element={
                    <RequireTemplatesAccess>
                      <CreateProjectFromTemplateWizardPage />
                    </RequireTemplatesAccess>
                  }
                />
                <Route
                  path="/templates/project/:id"
                  element={
                    <RequireTemplatesAccess>
                      <ProjectTemplateBuilderPage />
                    </RequireTemplatesAccess>
                  }
                />
                <Route
                  path="/templates/task/:id"
                  element={
                    <RequireTemplatesAccess>
                      <TaskTemplateBuilderPage />
                    </RequireTemplatesAccess>
                  }
                />
                <Route path="/settings" element={<SettingsLayout />}>
                  <Route index element={<ProfileSettingsPage />} />
                  <Route path="preferences" element={<PreferencesSettingsPage />} />
                  <Route path="notifications" element={<NotificationSettingsPage />} />
                  <Route path="dashboard" element={<DashboardSettingsPage />} />
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
                  <Route path="tags" element={<AdminTagsPage />} />
                  <Route path="file-categories" element={<AdminFileCategoriesPage />} />
                  <Route path="automations" element={<AdminAutomationsPage />} />
                  <Route path="settings" element={<AdminSystemSettingsPage />} />
                  <Route path="email" element={<AdminEmailSettingsPage />} />
                </Route>
              </Routes>
            </AppShell>
          </RequireAuth>
        }
      />
    </Routes>
  );
}
