import type { QuickFilter } from '@/utils/taskFilters';
import './MyTasksSummary.css';

interface MyTasksSummaryProps {
  summary: { total: number; open: number; dueToday: number; overdue: number; completed: number };
  quickFilter: QuickFilter;
  onQuickFilterChange: (filter: QuickFilter) => void;
}

const CARDS: { id: QuickFilter; label: string; key: keyof MyTasksSummaryProps['summary'] }[] = [
  { id: 'all', label: 'Total Tasks', key: 'total' },
  { id: 'open', label: 'Open Tasks', key: 'open' },
  { id: 'dueToday', label: 'Due Today', key: 'dueToday' },
  { id: 'overdue', label: 'Overdue', key: 'overdue' },
  { id: 'completed', label: 'Completed', key: 'completed' },
];

// Real counts computed from the already-loaded task list (useMyTasksFilters), never hard-coded.
// Each card doubles as a quick filter — clicking "Overdue" both shows the count and filters to it.
export function MyTasksSummary({ summary, quickFilter, onQuickFilterChange }: MyTasksSummaryProps) {
  return (
    <div className="my-tasks-summary">
      {CARDS.map((card) => (
        <button
          key={card.label}
          type="button"
          className={`my-tasks-summary__card${quickFilter === card.id && card.id !== 'all' ? ' my-tasks-summary__card--active' : ''}`}
          onClick={() => onQuickFilterChange(card.id)}
        >
          <span className="my-tasks-summary__value">{summary[card.key]}</span>
          <span className="my-tasks-summary__label">{card.label}</span>
        </button>
      ))}
    </div>
  );
}
