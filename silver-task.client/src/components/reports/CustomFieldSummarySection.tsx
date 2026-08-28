import { useState } from 'react';
import { ListFilter } from 'lucide-react';
import { useCustomFieldSummaryReport } from '@/hooks/useReports';
import { useCustomFields } from '@/hooks/useCustomFields';
import { useProjects } from '@/hooks/useProjects';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import { StatCard } from '@/components/dashboard/StatCard';
import { CUSTOM_FIELD_TYPE_LABELS, type CustomFieldEntityType, type CustomFieldType } from '@/types/customField';
import { ApiError } from '@/api/httpClient';

// Phase 41 — reuses the existing reporting engine (ReportingService.GetCustomFieldSummaryAsync)
// and existing project-scoped custom-field listing endpoint, rather than a second reporting
// engine or a new "list all my fields" endpoint. Scoped to one project at a time since this app
// has no non-Administrator "every field I can see, across every project" endpoint to draw a
// picker from — a disclosed, minor scope simplification (see the Phase 41 final report).
export function CustomFieldSummarySection() {
  const { data: projects } = useProjects();
  const [projectId, setProjectId] = useState('');
  const [entityType, setEntityType] = useState<CustomFieldEntityType>('Task');
  const [fieldId, setFieldId] = useState('');

  const { data: fields } = useCustomFields(projectId || undefined, entityType);
  const report = useCustomFieldSummaryReport(fieldId || undefined);

  return (
    <div className="report-section">
      <DashboardWidget title="Custom Field Summary" icon={<ListFilter size={14} />} isLoading={false} isError={false}>
        <div className="custom-field-summary__pickers">
          <select
            value={projectId}
            onChange={(e) => {
              setProjectId(e.target.value);
              setFieldId('');
            }}
          >
            <option value="">Select a project...</option>
            {projects?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
          <select
            value={entityType}
            onChange={(e) => {
              setEntityType(e.target.value as CustomFieldEntityType);
              setFieldId('');
            }}
          >
            <option value="Task">Task Fields</option>
            <option value="Project">Project Fields</option>
          </select>
          <select value={fieldId} onChange={(e) => setFieldId(e.target.value)} disabled={!projectId}>
            <option value="">Select a field...</option>
            {fields?.map((f) => (
              <option key={f.id} value={f.id}>
                {f.name} ({CUSTOM_FIELD_TYPE_LABELS[f.fieldType as CustomFieldType]})
              </option>
            ))}
          </select>
        </div>

        {report.isLoading && <p>Loading summary...</p>}
        {report.isError && (
          <p className="form-error">
            {report.error instanceof ApiError ? report.error.message : 'Could not load this field’s summary.'}
          </p>
        )}

        {report.data && (
          <div className="report-section__stats">
            <StatCard label="Values Set" value={report.data.count} />
            {report.data.sum !== null && (
              <>
                <StatCard label="Average" value={report.data.average ?? 0} />
                <StatCard label="Minimum" value={report.data.min ?? 0} />
                <StatCard label="Maximum" value={report.data.max ?? 0} />
                <StatCard label="Sum" value={report.data.sum} />
              </>
            )}
            {report.data.byValue && report.data.byValue.length > 0 && (
              <table className="report-table">
                <thead>
                  <tr>
                    <th scope="col">Value</th>
                    <th scope="col">Count</th>
                  </tr>
                </thead>
                <tbody>
                  {report.data.byValue.map((row) => (
                    <tr key={row.label}>
                      <td>{row.label}</td>
                      <td>{row.count}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        )}
      </DashboardWidget>
    </div>
  );
}
