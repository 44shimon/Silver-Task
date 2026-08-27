import { useState, type FormEvent } from 'react';
import { Copy, Save, Share2, Star, Trash2 } from 'lucide-react';
import {
  useCreateSavedReport,
  useDeleteSavedReport,
  useDuplicateSavedReport,
  useSavedReports,
  useShareSavedReport,
  useToggleSavedReportFavorite,
  useUnshareSavedReport,
} from '@/hooks/useReports';
import { usePermissions } from '@/hooks/usePermissions';
import { Permissions } from '@/types/permissions';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import { REPORT_TYPE_LABELS, type ReportConfiguration, type ReportFilters, type ReportGroupByField, type ReportType, type SavedReport } from '@/types/reports';
import { ApiError } from '@/api/httpClient';
import './MyReportsSection.css';

interface MyReportsSectionProps {
  currentReportType: ReportType;
  currentFilters: ReportFilters;
  currentGroupBy: ReportGroupByField;
  onOpenSavedReport: (config: ReportConfiguration) => void;
}

// "My Reports" — Saved Reports list + a minimal "save the current view" form. Sharing is
// deliberately narrow (explicit email-to-user only, no bulk project/role sharing — a disclosed
// scope cut, see SavedReportShare's own doc comment); the report's actual security boundary never
// depends on how it was shared, since Execute always re-checks the CURRENT viewer's live project
// access (see SavedReportsController.Execute's own doc comment) regardless of what this list shows.
export function MyReportsSection({ currentReportType, currentFilters, currentGroupBy, onOpenSavedReport }: MyReportsSectionProps) {
  const { can } = usePermissions();
  const reports = useSavedReports();
  const createReport = useCreateSavedReport();
  const deleteReport = useDeleteSavedReport();
  const duplicateReport = useDuplicateSavedReport();
  const shareReport = useShareSavedReport();
  const unshareReport = useUnshareSavedReport();
  const toggleFavorite = useToggleSavedReportFavorite();

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [sharingId, setSharingId] = useState<string | null>(null);
  const [shareEmail, setShareEmail] = useState('');

  function handleSave(event: FormEvent) {
    event.preventDefault();
    const trimmed = name.trim();
    if (!trimmed) return;

    const configuration: ReportConfiguration = {
      reportType: currentReportType,
      ...(currentReportType === 'Custom' ? { groupBy: currentGroupBy } : {}),
      dateRange: currentFilters.dateRange,
      startDate: currentFilters.startDate,
      endDate: currentFilters.endDate,
      projectId: currentFilters.projectId,
      userId: currentFilters.userId,
      status: currentFilters.status,
      priority: currentFilters.priority,
      labelId: currentFilters.labelId,
    };

    createReport.mutate(
      { name: trimmed, description: description.trim() || undefined, projectId: currentFilters.projectId, configuration: JSON.stringify(configuration) },
      { onSuccess: () => { setName(''); setDescription(''); } },
    );
  }

  function handleShare(event: FormEvent, id: string) {
    event.preventDefault();
    const email = shareEmail.trim();
    if (!email) return;
    shareReport.mutate(
      { id, email },
      { onSuccess: () => { setSharingId(null); setShareEmail(''); } },
    );
  }

  function openReport(report: SavedReport) {
    try {
      const config = JSON.parse(report.configuration) as ReportConfiguration;
      onOpenSavedReport(config);
    } catch {
      // Malformed configuration (shouldn't happen — the backend validates on save) — do nothing.
    }
  }

  return (
    <div className="report-section">
      {can(Permissions.ReportsCreate) && (
        <DashboardWidget title="Save Current View" icon={<Save size={14} />} isLoading={false} isError={false}>
          <form className="my-reports__save-form" onSubmit={handleSave}>
            <input
              type="text"
              placeholder="Report name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
            />
            <input
              type="text"
              placeholder="Description (optional)"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
            <button type="submit" disabled={createReport.isPending || !name.trim()}>
              Save "{REPORT_TYPE_LABELS[currentReportType]}" with current filters
            </button>
            {createReport.isError && (
              <p className="my-reports__error">
                {createReport.error instanceof ApiError ? createReport.error.message : 'Could not save report.'}
              </p>
            )}
          </form>
        </DashboardWidget>
      )}

      <DashboardWidget
        title="My Reports"
        isLoading={reports.isLoading}
        isError={reports.isError}
        onRetry={() => reports.refetch()}
        isEmpty={reports.data?.length === 0}
        emptyTitle="No saved reports yet"
        emptyMessage="Save a report above to see it here."
      >
        {reports.data && reports.data.length > 0 && (
          <ul className="my-reports__list">
            {reports.data.map((report) => (
              <li key={report.id} className="my-reports__item">
                <div className="my-reports__item-main">
                  <button
                    type="button"
                    className="my-reports__favorite"
                    aria-label={report.isFavorite ? 'Unfavorite' : 'Favorite'}
                    onClick={() => toggleFavorite.mutate({ id: report.id, favorite: !report.isFavorite })}
                  >
                    <Star size={14} fill={report.isFavorite ? 'currentColor' : 'none'} />
                  </button>
                  <button type="button" className="my-reports__name" onClick={() => openReport(report)}>
                    {report.name}
                  </button>
                  <span className="my-reports__type">{REPORT_TYPE_LABELS[report.reportType as ReportType] ?? report.reportType}</span>
                  {report.projectName && <span className="my-reports__project">{report.projectName}</span>}
                  {!report.isOwnedByMe && <span className="my-reports__owner">by {report.createdByName}</span>}
                </div>

                {report.description && <p className="my-reports__description">{report.description}</p>}

                {report.isOwnedByMe && report.sharedWith && report.sharedWith.length > 0 && (
                  <div className="my-reports__shares">
                    Shared with:
                    {report.sharedWith.map((s) => (
                      <span key={s.userId} className="my-reports__share-chip">
                        {s.name}
                        <button type="button" aria-label={`Unshare from ${s.name}`} onClick={() => unshareReport.mutate({ id: report.id, userId: s.userId })}>
                          ×
                        </button>
                      </span>
                    ))}
                  </div>
                )}

                <div className="my-reports__actions">
                  <button type="button" onClick={() => duplicateReport.mutate(report.id)}>
                    <Copy size={12} /> Duplicate
                  </button>
                  {report.isOwnedByMe && (
                    <button type="button" onClick={() => setSharingId(sharingId === report.id ? null : report.id)}>
                      <Share2 size={12} /> Share
                    </button>
                  )}
                  {report.isOwnedByMe && (
                    <button
                      type="button"
                      className="my-reports__delete"
                      onClick={() => {
                        if (window.confirm(`Delete "${report.name}"? This cannot be undone.`)) {
                          deleteReport.mutate(report.id);
                        }
                      }}
                    >
                      <Trash2 size={12} /> Delete
                    </button>
                  )}
                </div>

                {sharingId === report.id && (
                  <form className="my-reports__share-form" onSubmit={(e) => handleShare(e, report.id)}>
                    <input
                      type="email"
                      placeholder="user@example.com"
                      value={shareEmail}
                      onChange={(e) => setShareEmail(e.target.value)}
                      required
                    />
                    <button type="submit" disabled={shareReport.isPending}>
                      Share
                    </button>
                    {shareReport.isError && (
                      <p className="my-reports__error">
                        {shareReport.error instanceof ApiError ? shareReport.error.message : 'Could not share report.'}
                      </p>
                    )}
                  </form>
                )}
              </li>
            ))}
          </ul>
        )}
      </DashboardWidget>
    </div>
  );
}
