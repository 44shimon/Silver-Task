import { useAdminAutomations } from '@/hooks/useAutomations';
import { useAdminUsers } from '@/hooks/useAdminUsers';
import { AutomationList } from '@/components/automation/AutomationList';
import './AdminAutomationsPage.css';

/** Admin -> Automations — global (ProjectId-null) automations only, applying system-wide.
 * Project-scoped automations live under each project's own "Automations" tab instead (see
 * ProjectPage's view === 'automations' branch), matching how Custom Fields/Tags/File Categories
 * are already split between a project-scoped surface and this Admin-only global one. */
export function AdminAutomationsPage() {
  const { data: automations, isLoading } = useAdminAutomations();
  const { data: users } = useAdminUsers();

  return (
    <div className="admin-automations-page">
      <h1>Automations</h1>
      <p className="admin-automations-page__hint">
        Global automations apply across every project. Project-level automations are managed from each project&rsquo;s own
        Automations tab.
      </p>
      <AutomationList
        automations={automations ?? []}
        isLoading={isLoading}
        projectId={null}
        users={users?.filter((u) => u.isActive) ?? []}
        customFields={[]}
        canManage={true}
      />
    </div>
  );
}
