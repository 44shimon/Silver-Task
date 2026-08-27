import type { ReactNode } from 'react';
import { RotateCw } from 'lucide-react';
import './DashboardWidget.css';

interface DashboardWidgetProps {
  title: string;
  icon?: ReactNode;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
  isEmpty?: boolean;
  emptyTitle?: string;
  emptyMessage?: string;
  headerAction?: ReactNode;
  children: ReactNode;
}

// The one shared shell every dashboard widget renders through (Phase 37) — standardizes
// loading/error/empty presentation so a failure in one widget (e.g. Recent Files) never takes
// down the rest of the dashboard, and so "no overdue tasks" reads as a real empty state rather
// than a blank box. Widgets whose data comes from the single GET /api/dashboard query naturally
// share one loading/error state (they're one query — see hooks/useDashboard's own doc comment on
// why that's an intentional grouping, not an accidental one); widgets with their own independent
// query (Notifications/Recent Files/Team Workload) get fully isolated states here.
export function DashboardWidget({
  title,
  icon,
  isLoading,
  isError,
  onRetry,
  isEmpty,
  emptyTitle,
  emptyMessage,
  headerAction,
  children,
}: DashboardWidgetProps) {
  return (
    <section className="dashboard-widget" aria-labelledby={`widget-${title.replace(/\s+/g, '-').toLowerCase()}`}>
      <div className="dashboard-widget__header">
        <h2 id={`widget-${title.replace(/\s+/g, '-').toLowerCase()}`}>
          {icon}
          <span>{title}</span>
        </h2>
        {headerAction}
      </div>

      <div className="dashboard-widget__body">
        {isLoading && (
          <div className="dashboard-widget__skeleton" aria-live="polite" aria-busy="true">
            <span className="dashboard-widget__skeleton-line" />
            <span className="dashboard-widget__skeleton-line" />
            <span className="dashboard-widget__skeleton-line" />
          </div>
        )}

        {!isLoading && isError && (
          <div className="dashboard-widget__error">
            <p>Unable to load {title.toLowerCase()}.</p>
            {onRetry && (
              <button type="button" onClick={onRetry}>
                <RotateCw size={13} />
                Retry
              </button>
            )}
          </div>
        )}

        {!isLoading && !isError && isEmpty && (
          <div className="dashboard-widget__empty">
            <p className="dashboard-widget__empty-title">{emptyTitle ?? 'Nothing here'}</p>
            {emptyMessage && <p className="dashboard-widget__empty-message">{emptyMessage}</p>}
          </div>
        )}

        {!isLoading && !isError && !isEmpty && children}
      </div>
    </section>
  );
}
