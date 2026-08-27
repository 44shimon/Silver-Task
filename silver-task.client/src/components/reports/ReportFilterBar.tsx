import { useProjectMembers, useProjects } from '@/hooks/useProjects';
import { useActiveTags } from '@/hooks/useTags';
import { DATE_RANGE_OPTIONS } from '@/types/reports';
import type { ReportFilters } from '@/types/reports';
import { STATUS_OPTIONS, STATUS_LABELS, PRIORITY_OPTIONS } from '@/types/task';
import './ReportFilterBar.css';

interface ReportFilterBarProps {
  filters: ReportFilters;
  onChange: (filters: ReportFilters) => void;
}

// The one closed filter set every report tab shares — Date Range/Project/User/Status/Priority/
// Label, mirroring ReportFilterRequest.cs exactly. Project options are whatever useProjects()
// already returns (the caller's own accessible projects — never a free-text id), and the User
// filter is scoped to the SELECTED project's own members, so this can never even present an id
// for a project/user the caller can't see, let alone submit one that would matter (the backend
// re-scopes everything again regardless — see ReportsController's own doc comment).
export function ReportFilterBar({ filters, onChange }: ReportFilterBarProps) {
  const { data: projects } = useProjects();
  const { data: members } = useProjectMembers(filters.projectId);
  const { data: tags } = useActiveTags();

  function set<K extends keyof ReportFilters>(key: K, value: ReportFilters[K]) {
    onChange({ ...filters, [key]: value, page: 1 });
  }

  return (
    <div className="report-filter-bar">
      <label className="report-filter-bar__field">
        <span>Date Range</span>
        <select
          value={filters.dateRange ?? 'thisMonth'}
          onChange={(e) => set('dateRange', e.target.value as ReportFilters['dateRange'])}
        >
          {DATE_RANGE_OPTIONS.map((opt) => (
            <option key={opt.value} value={opt.value}>
              {opt.label}
            </option>
          ))}
        </select>
      </label>

      {filters.dateRange === 'custom' && (
        <>
          <label className="report-filter-bar__field">
            <span>Start</span>
            <input type="date" value={filters.startDate ?? ''} onChange={(e) => set('startDate', e.target.value || undefined)} />
          </label>
          <label className="report-filter-bar__field">
            <span>End</span>
            <input type="date" value={filters.endDate ?? ''} onChange={(e) => set('endDate', e.target.value || undefined)} />
          </label>
        </>
      )}

      <label className="report-filter-bar__field">
        <span>Project</span>
        <select
          value={filters.projectId ?? ''}
          onChange={(e) => onChange({ ...filters, projectId: e.target.value || undefined, userId: undefined, page: 1 })}
        >
          <option value="">All Projects</option>
          {projects?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
      </label>

      <label className="report-filter-bar__field">
        <span>Assignee</span>
        <select value={filters.userId ?? ''} onChange={(e) => set('userId', e.target.value || undefined)} disabled={!filters.projectId}>
          <option value="">{filters.projectId ? 'All Assignees' : 'Select a project first'}</option>
          {members?.map((m) => (
            <option key={m.user.id} value={m.user.id}>
              {m.user.name}
            </option>
          ))}
        </select>
      </label>

      <label className="report-filter-bar__field">
        <span>Status</span>
        <select value={filters.status ?? ''} onChange={(e) => set('status', (e.target.value || undefined) as ReportFilters['status'])}>
          <option value="">All Statuses</option>
          {STATUS_OPTIONS.map((s) => (
            <option key={s} value={s}>
              {STATUS_LABELS[s]}
            </option>
          ))}
        </select>
      </label>

      <label className="report-filter-bar__field">
        <span>Priority</span>
        <select value={filters.priority ?? ''} onChange={(e) => set('priority', (e.target.value || undefined) as ReportFilters['priority'])}>
          <option value="">All Priorities</option>
          {PRIORITY_OPTIONS.map((p) => (
            <option key={p} value={p}>
              {p}
            </option>
          ))}
        </select>
      </label>

      <label className="report-filter-bar__field">
        <span>Label</span>
        <select value={filters.labelId ?? ''} onChange={(e) => set('labelId', e.target.value || undefined)}>
          <option value="">All Labels</option>
          {tags?.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
      </label>
    </div>
  );
}
