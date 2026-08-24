import type { QuickFilter } from '@/hooks/useMyTasksFilters';
import './MyTasksQuickFilters.css';

interface MyTasksQuickFiltersProps {
  value: QuickFilter;
  onChange: (filter: QuickFilter) => void;
}

// Distinct from the summary cards above (which surface Total/Open/Due Today/Overdue/Completed
// as counts) — "Due This Week" only exists here, as a pure quick filter with no dedicated count.
const CHIPS: { id: QuickFilter; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'dueToday', label: 'Due Today' },
  { id: 'dueThisWeek', label: 'Due This Week' },
  { id: 'overdue', label: 'Overdue' },
  { id: 'completed', label: 'Completed' },
];

export function MyTasksQuickFilters({ value, onChange }: MyTasksQuickFiltersProps) {
  return (
    <div className="my-tasks-quick-filters" role="tablist">
      {CHIPS.map((chip) => (
        <button
          key={chip.id}
          type="button"
          role="tab"
          aria-selected={value === chip.id}
          className={`my-tasks-quick-filters__chip${value === chip.id ? ' my-tasks-quick-filters__chip--active' : ''}`}
          onClick={() => onChange(chip.id)}
        >
          {chip.label}
        </button>
      ))}
    </div>
  );
}
