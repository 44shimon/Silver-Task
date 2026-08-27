import { useMemo, useState } from 'react';
import { Plus, Zap } from 'lucide-react';
import type { UserSummary } from '@/types/project';
import type { CustomField } from '@/types/customField';
import { TRIGGER_TYPE_LABELS, type Automation } from '@/types/automation';
import { useDeleteAutomation, useDuplicateAutomation, useSetAutomationActive } from '@/hooks/useAutomations';
import { AutomationBuilder } from './AutomationBuilder';
import { AutomationRunsDialog } from './AutomationRunsDialog';
import { ConfirmDeleteDialog } from '@/components/shared/ConfirmDeleteDialog';
import './AutomationList.css';

interface AutomationListProps {
  automations: Automation[];
  isLoading: boolean;
  /** Null for the Admin -> Automations page (global automations). */
  projectId: string | null;
  users: UserSummary[];
  customFields: CustomField[];
  canManage: boolean;
}

export function AutomationList({ automations, isLoading, projectId, users, customFields, canManage }: AutomationListProps) {
  const setActive = useSetAutomationActive(projectId ?? undefined);
  const duplicate = useDuplicateAutomation(projectId ?? undefined);
  const deleteAutomation = useDeleteAutomation(projectId ?? undefined);
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<Automation | null>(null);
  const [creating, setCreating] = useState(false);
  const [viewingRuns, setViewingRuns] = useState<Automation | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState<Automation | null>(null);

  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) return automations;
    return automations.filter(
      (a) => a.name.toLowerCase().includes(query) || TRIGGER_TYPE_LABELS[a.triggerType].toLowerCase().includes(query),
    );
  }, [automations, search]);

  function handleDelete(automation: Automation) {
    deleteAutomation.mutate(automation.id, { onSuccess: () => setConfirmingDelete(null) });
  }

  return (
    <div className="automation-list">
      <div className="automation-list__toolbar">
        <input
          type="text"
          placeholder="Search automations..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="automation-list__search"
        />
        {canManage && (
          <button type="button" className="automation-list__new" onClick={() => setCreating(true)}>
            <Plus size={14} />
            New Automation
          </button>
        )}
      </div>

      {isLoading && <p>Loading automations...</p>}

      {!isLoading && filtered.length === 0 && (
        <div className="automation-list__empty">
          <Zap size={20} />
          <p>No automations yet. Automations react to events like task creation or status changes and run actions for you.</p>
        </div>
      )}

      {!isLoading && filtered.length > 0 && (
        <table className="automation-list__table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Status</th>
              <th>Trigger</th>
              <th>Last Run</th>
              <th>Runs</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {filtered.map((automation) => (
              <tr key={automation.id}>
                <td>
                  <span className="automation-list__name">{automation.name}</span>
                  {automation.lastError && <span className="automation-list__error-flag" title={automation.lastError}>⚠ Last run failed</span>}
                </td>
                <td>
                  <span className={`automation-list__status${automation.isActive ? '' : ' automation-list__status--disabled'}`}>
                    {automation.isActive ? 'Active' : 'Disabled'}
                  </span>
                </td>
                <td>{TRIGGER_TYPE_LABELS[automation.triggerType]}</td>
                <td>{automation.lastRunAt ? new Date(automation.lastRunAt).toLocaleString() : 'Never'}</td>
                <td>
                  <button type="button" className="automation-list__runs-link" onClick={() => setViewingRuns(automation)}>
                    {automation.runCount}
                  </button>
                </td>
                <td>
                  <div className="automation-list__actions">
                    {canManage && (
                      <button type="button" onClick={() => setEditing(automation)}>
                        Edit
                      </button>
                    )}
                    {canManage && (
                      <button
                        type="button"
                        onClick={() => setActive.mutate({ id: automation.id, isActive: !automation.isActive })}
                        disabled={setActive.isPending}
                      >
                        {automation.isActive ? 'Disable' : 'Enable'}
                      </button>
                    )}
                    {canManage && (
                      <button type="button" onClick={() => duplicate.mutate(automation.id)} disabled={duplicate.isPending}>
                        Duplicate
                      </button>
                    )}
                    {canManage && (
                      <button type="button" onClick={() => setConfirmingDelete(automation)}>
                        Delete
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {(creating || editing) && (
        <AutomationBuilder
          projectId={projectId}
          users={users}
          customFields={customFields}
          automation={editing}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
        />
      )}

      {viewingRuns && (
        <AutomationRunsDialog
          automationId={viewingRuns.id}
          automationName={viewingRuns.name}
          canRetry={canManage}
          onClose={() => setViewingRuns(null)}
        />
      )}

      {confirmingDelete && (
        <ConfirmDeleteDialog
          title={`Delete "${confirmingDelete.name}"?`}
          message="This automation will stop running. Its execution history (Runs) is kept."
          isDeleting={deleteAutomation.isPending}
          onClose={() => setConfirmingDelete(null)}
          onConfirmDelete={() => handleDelete(confirmingDelete)}
        />
      )}
    </div>
  );
}
