import type { Task } from '@/types/task';
import type { CustomField } from '@/types/customField';
import type { UserSummary } from '@/types/project';
import { useTasks } from '@/hooks/useTasks';
import { useProjects } from '@/hooks/useProjects';
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
  // Task/Project reference pickers reuse whatever's already cached under the same query key
  // TaskTable/ProjectPage populate (['projects', projectId, 'tasks'] / ['projects']) — this is a
  // cache read, not an extra network round trip, so it's safe to call unconditionally here even
  // though only two of the many field types below actually use the result.
  const { data: projectTasks } = useTasks(field.fieldType === 'TaskReference' ? projectId : undefined);
  const { data: allProjects } = useProjects();

  switch (field.fieldType) {
    case 'Text':
    case 'LongText':
    case 'Number':
    case 'Currency':
    case 'Url':
    case 'Email':
    case 'Phone':
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

    case 'UserMulti':
      return (
        <MultiSelectCustomValueCell
          task={task}
          field={field}
          projectId={projectId}
          value={value}
          options={members.map((m) => ({ id: m.id, value: m.name }))}
          emptyMessage="No project members."
        />
      );

    case 'TaskReference':
      return (
        <SelectCustomValueCell
          task={task}
          field={field}
          projectId={projectId}
          value={value}
          options={(projectTasks ?? []).filter((t) => t.id !== task.id).map((t) => ({ id: t.id, label: t.title }))}
        />
      );

    case 'ProjectReference':
      return (
        <SelectCustomValueCell
          task={task}
          field={field}
          projectId={projectId}
          value={value}
          options={(allProjects ?? []).map((p) => ({ id: p.id, label: p.name }))}
        />
      );

    case 'Link':
      return <LinkCustomValueCell task={task} field={field} projectId={projectId} value={value} />;

    default:
      return null;
  }
}
