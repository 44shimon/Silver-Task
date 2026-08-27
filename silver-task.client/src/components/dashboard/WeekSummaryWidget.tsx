import { TrendingUp } from 'lucide-react';
import type { StatsRange, WeekSummary } from '@/types/dashboard';
import { DashboardWidget } from './DashboardWidget';
import './WeekSummaryWidget.css';

interface WeekSummaryWidgetProps {
  summary: WeekSummary;
  range: StatsRange;
  onRangeChange: (range: StatsRange) => void;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

const RANGE_OPTIONS: { id: StatsRange; label: string }[] = [
  { id: 'today', label: 'Today' },
  { id: 'week', label: 'This Week' },
  { id: 'month', label: 'This Month' },
];

const RANGE_TITLE: Record<StatsRange, string> = { today: 'Today', week: 'This Week', month: 'This Month' };

export function WeekSummaryWidget({ summary, range, onRangeChange, isLoading, isError, onRetry }: WeekSummaryWidgetProps) {
  const percent = Math.round(summary.completionRate * 100);

  return (
    <DashboardWidget
      title={RANGE_TITLE[range]}
      icon={<TrendingUp size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      headerAction={
        <select
          className="dashboard-widget__range-select"
          value={range}
          onChange={(e) => onRangeChange(e.target.value as StatsRange)}
          aria-label="Statistics range"
        >
          {RANGE_OPTIONS.map((option) => (
            <option key={option.id} value={option.id}>
              {option.label}
            </option>
          ))}
        </select>
      }
    >
      <div className="week-summary-widget">
        <dl className="week-summary-widget__stats">
          <div>
            <dt>Assigned</dt>
            <dd>{summary.assignedCount}</dd>
          </div>
          <div>
            <dt>Completed</dt>
            <dd>{summary.completedCount}</dd>
          </div>
          <div>
            <dt>Remaining</dt>
            <dd>{summary.remainingCount}</dd>
          </div>
          <div>
            <dt>Overdue</dt>
            <dd className={summary.overdueCount > 0 ? 'week-summary-widget__overdue' : undefined}>{summary.overdueCount}</dd>
          </div>
        </dl>

        <div className="week-summary-widget__completion">
          <div className="week-summary-widget__completion-bar">
            <div className="week-summary-widget__completion-fill" style={{ width: `${percent}%` }} />
          </div>
          <span className="week-summary-widget__completion-label">{percent}% completion</span>
        </div>
      </div>
    </DashboardWidget>
  );
}
