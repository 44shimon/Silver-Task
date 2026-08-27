import { Download } from 'lucide-react';
import { reportsApi } from '@/api/reportsApi';
import { usePermissions } from '@/hooks/usePermissions';
import { Permissions } from '@/types/permissions';
import type { ExportFormat, ReportFilters, ReportType } from '@/types/reports';
import './ExportButtons.css';

interface ExportButtonsProps {
  reportType: ReportType;
  filters: ReportFilters;
  extra?: Record<string, string | number | undefined>;
}

const FORMATS: { value: ExportFormat; label: string }[] = [
  { value: 'csv', label: 'CSV' },
  { value: 'excel', label: 'Excel' },
  { value: 'pdf', label: 'PDF' },
];

// Export applies exactly the same authorization + query path as the on-screen report — see
// ReportsController.Export's own doc comment; this is never a separate/weaker endpoint. A plain
// same-origin <a href> download, same pattern as attachmentsApi.downloadUrl — the browser sends
// the auth cookie automatically, no separate fetch/blob handling needed.
export function ExportButtons({ reportType, filters, extra }: ExportButtonsProps) {
  const { can } = usePermissions();
  if (!can(Permissions.ReportsExport)) {
    return null;
  }

  return (
    <div className="export-buttons">
      <Download size={13} />
      {FORMATS.map((f) => (
        <a
          key={f.value}
          className="export-buttons__link"
          href={reportsApi.exportUrl(reportType, filters, f.value, extra)}
          target="_blank"
          rel="noopener noreferrer"
        >
          {f.label}
        </a>
      ))}
    </div>
  );
}
