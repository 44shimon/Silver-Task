import { useNavigate } from 'react-router-dom';
import { LayoutTemplate } from 'lucide-react';
import { useTemplateUsageReport } from '@/hooks/useReports';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import { formatDateTime } from '@/utils/formatDate';

// Phase 40 — reuses the existing reporting engine (ReportingService.GetTemplateUsageReportAsync)
// rather than a second one; scoped to the caller's own accessible projects/templates, same as
// every other report tab.
export function TemplateUsageSection() {
  const report = useTemplateUsageReport();
  const navigate = useNavigate();

  return (
    <div className="report-section">
      <DashboardWidget
        title="Template Usage"
        icon={<LayoutTemplate size={14} />}
        isLoading={report.isLoading}
        isError={report.isError}
        onRetry={() => report.refetch()}
        isEmpty={report.data?.mostUsedTemplates.length === 0}
        emptyTitle="No templates used yet"
        emptyMessage="Projects and tasks created from a template will show up here."
      >
        {report.data && (
          <>
            <div className="template-usage-section__summary">
              <span>{report.data.projectsCreatedFromTemplate} projects created from a template</span>
            </div>

            {report.data.mostUsedTemplates.length > 0 && (
              <table className="report-table">
                <thead>
                  <tr>
                    <th scope="col">Template</th>
                    <th scope="col">Type</th>
                    <th scope="col">Used</th>
                    <th scope="col">Last Used</th>
                  </tr>
                </thead>
                <tbody>
                  {report.data.mostUsedTemplates.map((row) => (
                    <tr
                      key={row.templateId}
                      className="report-table__row-link"
                      onClick={() => navigate(row.type === 'Project' ? `/templates/project/${row.templateId}` : `/templates/task/${row.templateId}`)}
                    >
                      <td>{row.templateName}</td>
                      <td>{row.type}</td>
                      <td>{row.usageCount}</td>
                      <td>{row.lastUsedAt ? formatDateTime(row.lastUsedAt) : '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}
      </DashboardWidget>
    </div>
  );
}
