import type { ChangeEvent } from 'react';
import { ChevronDown } from 'lucide-react';
import type { Task } from '@/types/task';
import type { UserSummary } from '@/types/project';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import './DropdownCell.css';

interface AssignedToDropdownCellProps {
  task: Task;
  projectId: string;
  /** Project members only (per spec) — not every system user. */
  members: UserSummary[];
}

const UNASSIGNED_VALUE = '';

export function AssignedToDropdownCell({ task, projectId, members }: AssignedToDropdownCellProps) {
  const updateTask = useUpdateTask(projectId);

  function handleChange(event: ChangeEvent<HTMLSelectElement>) {
    const userId = event.target.value;
    const member = userId ? (members.find((m) => m.id === userId) ?? null) : null;
    if ((member?.id ?? null) !== (task.assignedTo?.id ?? null)) {
      updateTask.mutate({ task, change: taskFieldChange.assignee(member) });
    }
  }

  // A deactivated/deleted member can't be picked as a *new* assignee (the backend rejects it
  // too — this just keeps the option out of the list), but if this task is already assigned to
  // one, keep them selectable here so the select still shows who it's actually assigned to
  // instead of silently falling back to blank.
  const selectableMembers = members.filter((m) => m.isActive || m.id === task.assignedTo?.id);

  return (
    <div className="dropdown-cell-wrapper dropdown-cell-wrapper--plain">
      <select
        className={`dropdown-cell dropdown-cell--plain${updateTask.isError ? ' dropdown-cell--error' : ''}`}
        value={task.assignedTo?.id ?? UNASSIGNED_VALUE}
        onChange={handleChange}
        disabled={updateTask.isPending}
        title={updateTask.isError ? 'Could not save — try again' : undefined}
      >
        <option value={UNASSIGNED_VALUE}>Unassigned</option>
        {selectableMembers.map((member) => (
          <option key={member.id} value={member.id}>
            {member.name}
            {member.isActive ? '' : ' (inactive)'}
          </option>
        ))}
      </select>
      <ChevronDown size={12} className="dropdown-cell__chevron" />
    </div>
  );
}
