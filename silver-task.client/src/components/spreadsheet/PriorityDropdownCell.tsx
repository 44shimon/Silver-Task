import type { ChangeEvent } from 'react';
import { ChevronDown } from 'lucide-react';
import { PRIORITY_OPTIONS, type Task, type TaskPriority } from '@/types/task';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import './DropdownCell.css';

interface PriorityDropdownCellProps {
  task: Task;
  projectId: string;
  readOnly?: boolean;
}

export function PriorityDropdownCell({ task, projectId, readOnly }: PriorityDropdownCellProps) {
  const updateTask = useUpdateTask(projectId);

  function handleChange(event: ChangeEvent<HTMLSelectElement>) {
    const newPriority = event.target.value as TaskPriority;
    if (newPriority !== task.priority) {
      updateTask.mutate({ task, change: taskFieldChange.priority(newPriority) });
    }
  }

  return (
    <div className="dropdown-cell-wrapper">
      <select
        className={`dropdown-cell dropdown-cell--badge dropdown-cell--priority-${task.priority.toLowerCase()}${updateTask.isError ? ' dropdown-cell--error' : ''}`}
        value={task.priority}
        onChange={handleChange}
        disabled={readOnly || updateTask.isPending}
        title={updateTask.isError ? 'Could not save — try again' : undefined}
      >
        {PRIORITY_OPTIONS.map((priority) => (
          <option key={priority} value={priority}>
            {priority}
          </option>
        ))}
      </select>
      <ChevronDown size={12} className="dropdown-cell__chevron" />
    </div>
  );
}
