import { ArrowDown, ArrowUp } from 'lucide-react';
import type { SortDirection, TaskSortField } from '@/hooks/useTaskFilters';

interface SortableColumnHeaderProps {
  label: string;
  field: TaskSortField;
  activeField: TaskSortField;
  direction: SortDirection;
  onClick: (field: TaskSortField) => void;
}

// A convenience shortcut for the columns that have headers — clicking drives the same
// sort state as the toolbar's Sort menu, which is the only way to sort by Created/Updated
// Date since those aren't rendered columns.
export function SortableColumnHeader({ label, field, activeField, direction, onClick }: SortableColumnHeaderProps) {
  const isActive = field === activeField;

  return (
    <button type="button" className="task-table__sort-header" onClick={() => onClick(field)}>
      <span>{label}</span>
      {isActive && (direction === 'asc' ? <ArrowUp size={12} /> : <ArrowDown size={12} />)}
    </button>
  );
}
