import type { Task } from '@/types/task';
import type { CustomField } from '@/types/customField';
import type { UserSummary } from '@/types/project';
import { TextCustomValueCell } from './TextCustomValueCell';
import { DateCustomValueCell } from './DateCustomValueCell';
import { CheckboxCustomValueCell } from './CheckboxCustomValueCell';
import { SelectCustomValueCell } from './SelectCustomValueCell';
import { MultiSelectCustomValueCell } from './MultiSelectCustomValueCell';
import { LinkCustomValueCell } from './LinkCustomValueCell';

interface CustomFieldCellProps {
  task: Task;
  field: CustomField;
  projectId: string;
  members: UserSummary[];
}

export function CustomFieldCell({ task, field, projectId, members }: CustomFieldCellProps) {
  const value = task.customValues.find((v) => v.customFieldId === field.id)?.value ?? null;

  switch (field.fieldType) {
    case 'Text':
    case 'LongText':
    case 'Number':
    case 'Currency':
      return <TextCustomValueCell task={task} field={field} projectId={projectId} value={value} />;

    case 'Date':
    case 'DateTime':
      return <DateCustomValueCell task={task} field={field} projectId={projectId} value={value} />;

    case 'Checkbox':
      return <CheckboxCustomValueCell task={task} field={field} projectId={projectId} value={value} />;

    case 'Dropdown':
      return (
        <SelectCustomValueCell
          task={task}
          field={field}
          projectId={projectId}
          value={value}
          options={field.options.map((o) => ({ id: o.id, label: o.value }))}
        />
      );

    case 'MultiSelect':
      return <MultiSelectCustomValueCell task={task} field={field} projectId={projectId} value={value} />;

    case 'User':
      return (
        <SelectCustomValueCell
          task={task}
          field={field}
          projectId={projectId}
          value={value}
          options={members.map((m) => ({ id: m.id, label: m.name }))}
        />
      );

    case 'Link':
      return <LinkCustomValueCell task={task} field={field} projectId={projectId} value={value} />;

    default:
      return null;
  }
}
