import type { QuickFilter } from '@/utils/taskFilters';
import './QuickFilterChips.css';

interface QuickFilterChipsProps {
  value: QuickFilter;
  onChange: (filter: QuickFilter) => void;
}

// Shared by every view that filters tasks — the Project views (Table/Kanban/Calendar/Timeline/
// Gantt, via ProjectPage) and My Tasks both render this exact component, so "Overdue" or
// "Completed" means the same thing and looks the same everywhere rather than being five
// separate quick-filter implementations.
const CHIPS: { id: QuickFilter; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'dueToday', label: 'Due Today' },
  { id: 'dueThisWeek', label: 'Due This Week' },
  { id: 'overdue', label: 'Overdue' },
  { id: 'completed', label: 'Completed' },
];

export function QuickFilterChips({ value, onChange }: QuickFilterChipsProps) {
  return (
    <div className="quick-filter-chips" role="tablist">
      {CHIPS.map((chip) => (
        <button
          key={chip.id}
          type="button"
          role="tab"
          aria-selected={value === chip.id}
          className={`quick-filter-chips__chip${value === chip.id ? ' quick-filter-chips__chip--active' : ''}`}
          onClick={() => onChange(chip.id)}
        >
          {chip.label}
        </button>
      ))}
    </div>
  );
}
