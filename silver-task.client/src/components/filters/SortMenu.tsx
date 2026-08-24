import { ArrowUpDown } from 'lucide-react';
import type { SortDirection } from '@/utils/taskFilters';
import '@/components/spreadsheet/Toolbar.css';

interface SortMenuProps<TField extends string> {
  sortField: TField;
  sortDirection: SortDirection;
  fields: TField[];
  labels: Record<TField, string>;
  onFieldChange: (field: TField) => void;
  onDirectionChange: (direction: SortDirection) => void;
}

// Generic over the sort-field union (same "share the shell, vary the field type" approach as
// SortableColumnHeader) so the Project views and My Tasks — which sort by different field sets
// (Assigned To vs. Project) — use one sort menu implementation instead of two near-identical ones.
export function SortMenu<TField extends string>({
  sortField,
  sortDirection,
  fields,
  labels,
  onFieldChange,
  onDirectionChange,
}: SortMenuProps<TField>) {
  return (
    <details className="toolbar-popover">
      <summary className="toolbar-button">
        <ArrowUpDown size={14} />
        <span>Sort: {labels[sortField]}</span>
      </summary>
      <div className="toolbar-popover__panel">
        <label className="toolbar-popover__field">
          <span>Sort by</span>
          <select value={sortField} onChange={(e) => onFieldChange(e.target.value as TField)}>
            {fields.map((field) => (
              <option key={field} value={field}>
                {labels[field]}
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
