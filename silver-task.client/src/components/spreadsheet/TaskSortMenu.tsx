import { ArrowUpDown } from 'lucide-react';
import { SORT_FIELDS, SORT_FIELD_LABELS, type SortDirection, type TaskSortField } from '@/hooks/useTaskFilters';
import './Toolbar.css';

interface TaskSortMenuProps {
  sortField: TaskSortField;
  sortDirection: SortDirection;
  onFieldChange: (field: TaskSortField) => void;
  onDirectionChange: (direction: SortDirection) => void;
}

export function TaskSortMenu({ sortField, sortDirection, onFieldChange, onDirectionChange }: TaskSortMenuProps) {
  return (
    <details className="toolbar-popover">
      <summary className="toolbar-button">
        <ArrowUpDown size={14} />
        <span>Sort: {SORT_FIELD_LABELS[sortField]}</span>
      </summary>
      <div className="toolbar-popover__panel">
        <label className="toolbar-popover__field">
          <span>Sort by</span>
          <select value={sortField} onChange={(e) => onFieldChange(e.target.value as TaskSortField)}>
            {SORT_FIELDS.map((field) => (
              <option key={field} value={field}>
                {SORT_FIELD_LABELS[field]}
              </option>
            ))}
          </select>
        </label>

        <div className="toolbar-popover__direction">
          <button
            type="button"
            className={`toolbar-popover__direction-btn${sortDirection === 'asc' ? ' toolbar-popover__direction-btn--active' : ''}`}
            onClick={() => onDirectionChange('asc')}
          >
            Ascending
          </button>
          <button
            type="button"
            className={`toolbar-popover__direction-btn${sortDirection === 'desc' ? ' toolbar-popover__direction-btn--active' : ''}`}
            onClick={() => onDirectionChange('desc')}
          >
            Descending
          </button>
        </div>
      </div>
    </details>
  );
}
