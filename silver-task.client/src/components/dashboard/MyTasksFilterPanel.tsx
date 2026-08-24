import { Filter } from 'lucide-react';
import { PRIORITY_OPTIONS, STATUS_LABELS, STATUS_OPTIONS } from '@/types/task';
import type { Project } from '@/types/project';
import type { MyTasksFilters } from '@/hooks/useMyTasksFilters';
import '@/components/spreadsheet/Toolbar.css';

interface MyTasksFilterPanelProps {
  filters: MyTasksFilters;
  onChange: (filters: MyTasksFilters) => void;
  onClear: () => void;
  activeCount: number;
  projects: Project[];
}

export function MyTasksFilterPanel({ filters, onChange, onClear, activeCount, projects }: MyTasksFilterPanelProps) {
  return (
    <details className="toolbar-popover">
      <summary className="toolbar-button">
        <Filter size={14} />
        <span>Filter{activeCount > 0 ? ` (${activeCount})` : ''}</span>
      </summary>
      <div className="toolbar-popover__panel">
        <label className="toolbar-popover__field">
          <span>Project</span>
          <select
            value={filters.projectId ?? ''}
            onChange={(e) => onChange({ ...filters, projectId: e.target.value || null })}
          >
            <option value="">All projects</option>
            {projects.map((project) => (
              <option key={project.id} value={project.id}>
                {project.name}
              </option>
            ))}
          </select>
        </label>

        <label className="toolbar-popover__field">
          <span>Status</span>
          <select
            value={filters.status ?? ''}
            onChange={(e) => onChange({ ...filters, status: (e.target.value || null) as MyTasksFilters['status'] })}
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
            onChange={(e) =>
              onChange({ ...filters, priority: (e.target.value || null) as MyTasksFilters['priority'] })
            }
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
          <span>Due before</span>
          <input
            type="date"
            value={filters.dueBefore ?? ''}
            onChange={(e) => onChange({ ...filters, dueBefore: e.target.value || null })}
          />
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
