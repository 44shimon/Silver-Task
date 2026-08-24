import { ArrowDown, ArrowUp } from 'lucide-react';
import type { SortDirection } from '@/utils/taskFilters';

// Generic over the sort-field union so both TaskTable (TaskSortField) and MyTasksTable
// (MyTaskSortField) can share this without either type depending on the other.
interface SortableColumnHeaderProps<TField extends string> {
  label: string;
  field: TField;
  activeField: TField;
  direction: SortDirection;
  onClick: (field: TField) => void;
}

// A convenience shortcut for the columns that have headers — clicking drives the same
// sort state as the toolbar's Sort menu, which is the only way to sort by Created/Updated
// Date since those aren't rendered columns.
export function SortableColumnHeader<TField extends string>({
  label,
  field,
  activeField,
  direction,
  onClick,
}: SortableColumnHeaderProps<TField>) {
  const isActive = field === activeField;

  return (
    <button type="button" className="task-table__sort-header" onClick={() => onClick(field)}>
      <span>{label}</span>
      {isActive && (direction === 'asc' ? <ArrowUp size={12} /> : <ArrowDown size={12} />)}
    </button>
  );
}
