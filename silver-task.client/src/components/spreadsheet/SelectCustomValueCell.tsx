import type { ChangeEvent } from 'react';
import { ChevronDown } from 'lucide-react';
import type { Task } from '@/types/task';
import type { CustomField } from '@/types/customField';
import { useSetTaskCustomValue } from '@/hooks/useTasks';
import './DropdownCell.css';

interface SelectOption {
  id: string;
  label: string;
}

interface SelectCustomValueCellProps {
  task: Task;
  field: CustomField;
  projectId: string;
  value: string | null;
  /** Dropdown fields pass their own options; User fields pass project members. */
  options: SelectOption[];
  placeholder?: string;
}

/** Handles Dropdown (options from the field definition) and User (options from
 * project members) — both are "pick one id from a list" with the same UI. */
export function SelectCustomValueCell({
  task,
  field,
  projectId,
  value,
  options,
  placeholder = 'None',
}: SelectCustomValueCellProps) {
  const setValue = useSetTaskCustomValue(projectId);

  function handleChange(event: ChangeEvent<HTMLSelectElement>) {
    const newValue = event.target.value || null;
    if (newValue !== value) {
      setValue.mutate({ task, customFieldId: field.id, value: newValue });
    }
  }

  return (
    <div className="dropdown-cell-wrapper dropdown-cell-wrapper--plain">
      <select
        className={`dropdown-cell dropdown-cell--plain${setValue.isError ? ' dropdown-cell--error' : ''}`}
        value={value ?? ''}
        onChange={handleChange}
        disabled={setValue.isPending}
        title={setValue.isError ? 'Could not save — try again' : undefined}
      >
        <option value="">{placeholder}</option>
        {options.map((option) => (
          <option key={option.id} value={option.id}>
            {option.label}
          </option>
        ))}
      </select>
      <ChevronDown size={12} className="dropdown-cell__chevron" />
    </div>
  );
}
