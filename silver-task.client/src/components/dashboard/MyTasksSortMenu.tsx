import { ArrowUpDown } from 'lucide-react';
import type { SortDirection } from '@/hooks/useTaskFilters';
import { MY_TASK_SORT_FIELDS, MY_TASK_SORT_FIELD_LABELS, type MyTaskSortField } from '@/hooks/useMyTasksFilters';
import '@/components/spreadsheet/Toolbar.css';

interface MyTasksSortMenuProps {
  sortField: MyTaskSortField;
  sortDirection: SortDirection;
  onFieldChange: (field: MyTaskSortField) => void;
  onDirectionChange: (direction: SortDirection) => void;
}

export function MyTasksSortMenu({ sortField, sortDirection, onFieldChange, onDirectionChange }: MyTasksSortMenuProps) {
  return (
    <details className="toolbar-popover">
      <summary className="toolbar-button">
        <ArrowUpDown size={14} />
        <span>Sort: {MY_TASK_SORT_FIELD_LABELS[sortField]}</span>
      </summary>
      <div className="toolbar-popover__panel">
        <label className="toolbar-popover__field">
          <span>Sort by</span>
          <select value={sortField} onChange={(e) => onFieldChange(e.target.value as MyTaskSortField)}>
            {MY_TASK_SORT_FIELDS.map((field) => (
              <option key={field} value={field}>
                {MY_TASK_SORT_FIELD_LABELS[field]}
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
