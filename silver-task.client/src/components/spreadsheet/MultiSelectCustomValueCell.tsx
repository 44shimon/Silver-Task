import type { Task } from '@/types/task';
import type { CustomField } from '@/types/customField';
import { useSetTaskCustomValue } from '@/hooks/useTasks';
import { ChevronDown } from 'lucide-react';
import './MultiSelectCustomValueCell.css';

interface MultiOption {
  id: string;
  value: string;
}

interface MultiSelectCustomValueCellProps {
  task: Task;
  field: CustomField;
  projectId: string;
  value: string | null;
  /** MultiSelect fields pass their own options (default); UserMulti fields pass project members. */
  options?: MultiOption[];
  emptyMessage?: string;
}

export function MultiSelectCustomValueCell({ task, field, projectId, value, options, emptyMessage }: MultiSelectCustomValueCellProps) {
  const setValue = useSetTaskCustomValue(projectId);
  const effectiveOptions = options ?? field.options;
  const selectedIds = value ? safeParseIds(value) : [];
  const selectedLabels = effectiveOptions.filter((o) => selectedIds.includes(o.id)).map((o) => o.value);

  function toggle(optionId: string) {
    const next = selectedIds.includes(optionId)
      ? selectedIds.filter((id) => id !== optionId)
      : [...selectedIds, optionId];
    setValue.mutate({ task, customFieldId: field.id, value: next.length > 0 ? JSON.stringify(next) : null });
  }

  return (
    <details className={`multiselect-cell${setValue.isError ? ' multiselect-cell--error' : ''}`}>
      <summary className="multiselect-cell__summary">
        <span className="multiselect-cell__label">
          {selectedLabels.length > 0 ? (
            selectedLabels.join(', ')
          ) : (
            <span className="editable-cell__placeholder">—</span>
          )}
        </span>
        <ChevronDown size={12} className="dropdown-cell__chevron" />
      </summary>
      <div className="multiselect-cell__panel">
        {effectiveOptions.map((option) => (
          <label key={option.id} className="multiselect-cell__option">
            <input type="checkbox" checked={selectedIds.includes(option.id)} onChange={() => toggle(option.id)} />
            <span>{option.value}</span>
          </label>
        ))}
        {effectiveOptions.length === 0 && <p className="multiselect-cell__empty">{emptyMessage ?? 'No options defined.'}</p>}
      </div>
    </details>
  );
}

function safeParseIds(value: string): string[] {
  try {
    const parsed: unknown = JSON.parse(value);
    return Array.isArray(parsed) ? (parsed as string[]) : [];
  } catch {
    return [];
  }
}
