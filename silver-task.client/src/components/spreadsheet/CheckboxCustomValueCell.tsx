import type { ChangeEvent } from 'react';
import type { Task } from '@/types/task';
import type { CustomField } from '@/types/customField';
import { useSetTaskCustomValue } from '@/hooks/useTasks';

interface CheckboxCustomValueCellProps {
  task: Task;
  field: CustomField;
  projectId: string;
  value: string | null;
}

export function CheckboxCustomValueCell({ task, field, projectId, value }: CheckboxCustomValueCellProps) {
  const setValue = useSetTaskCustomValue(projectId);
  const checked = value === 'true';

  function handleChange(event: ChangeEvent<HTMLInputElement>) {
    // Unchecking clears the value rather than storing "false" — no row means "not set",
    // consistent with how every other empty custom value is represented.
    setValue.mutate({ task, customFieldId: field.id, value: event.target.checked ? 'true' : null });
  }

  return (
    <input
      type="checkbox"
      checked={checked}
      onChange={handleChange}
      disabled={setValue.isPending}
      aria-label={field.name}
      title={setValue.isError ? 'Could not save — try again' : undefined}
    />
  );
}
