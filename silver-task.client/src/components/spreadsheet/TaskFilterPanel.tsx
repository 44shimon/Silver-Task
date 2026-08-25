import { Filter } from 'lucide-react';
import { PRIORITY_OPTIONS, STATUS_LABELS, STATUS_OPTIONS } from '@/types/task';
import type { UserSummary } from '@/types/project';
import type { TaskFilters } from '@/hooks/useTaskFilters';
import { DEPENDENCY_STATE_LABELS, type DependencyStateFilter } from '@/utils/taskFilters';
import './Toolbar.css';

interface TaskFilterPanelProps {
  filters: TaskFilters;
  onChange: (filters: TaskFilters) => void;
  onClear: () => void;
  activeCount: number;
  members: UserSummary[];
}

export function TaskFilterPanel({ filters, onChange, onClear, activeCount, members }: TaskFilterPanelProps) {
  return (
    <details className="toolbar-popover">
      <summary className="toolbar-button">
        <Filter size={14} />
        <span>Filter{activeCount > 0 ? ` (${activeCount})` : ''}</span>
      </summary>
      <div className="toolbar-popover__panel">
        <label className="toolbar-popover__field">
          <span>Status</span>
          <select
            value={filters.status ?? ''}
            onChange={(e) => onChange({ ...filters, status: (e.target.value || null) as TaskFilters['status'] })}
          >
            <option value="">All statuses</option>
            {STATUS_OPTIONS.map((status) => (
              <option key={status} value={status}>
                {STATUS_LABELS[status]}
              </option>
            ))}
          </select>
        </label>

        <label className="toolbar-popover__field">
          <span>Priority</span>
          <select
            value={filters.priority ?? ''}
            onChange={(e) => onChange({ ...filters, priority: (e.target.value || null) as TaskFilters['priority'] })}
          >
            <option value="">All priorities</option>
            {PRIORITY_OPTIONS.map((priority) => (
              <option key={priority} value={priority}>
                {priority}
              </option>
            ))}
          </select>
        </label>

        <label className="toolbar-popover__field">
          <span>Assigned To</span>
          <select
            value={filters.assigneeId ?? ''}
            onChange={(e) => onChange({ ...filters, assigneeId: e.target.value || null })}
          >
            <option value="">Anyone</option>
            <option value="unassigned">Unassigned</option>
            {members.map((member) => (
              <option key={member.id} value={member.id}>
                {member.name}
              </option>
            ))}
          </select>
        </label>

        <label className="toolbar-popover__field">
          <span>Due before</span>
          <input
            type="date"
            value={filters.dueBefore ?? ''}
            onChange={(e) => onChange({ ...filters, dueBefore: e.target.value || null })}
          />
        </label>

        <label className="toolbar-popover__field">
          <span>Dependency state</span>
          <select
            value={filters.dependencyState ?? ''}
            onChange={(e) =>
              onChange({ ...filters, dependencyState: (e.target.value || null) as DependencyStateFilter | null })
            }
          >
            <option value="">All</option>
            {(Object.keys(DEPENDENCY_STATE_LABELS) as DependencyStateFilter[]).map((state) => (
              <option key={state} value={state}>
                {DEPENDENCY_STATE_LABELS[state]}
              </option>
            ))}
          </select>
        </label>

        {activeCount > 0 && (
          <button type="button" className="toolbar-popover__clear" onClick={onClear}>
            Clear filters
          </button>
        )}
      </div>
    </details>
  );
}
