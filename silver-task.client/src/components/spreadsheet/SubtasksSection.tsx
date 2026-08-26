import { useState, type FormEvent, type MouseEvent } from 'react';
import { Check } from 'lucide-react';
import type { Task, TaskPriority } from '@/types/task';
import { PRIORITY_OPTIONS } from '@/types/task';
import type { UserSummary } from '@/types/project';
import { useCreateSubtask, useSubtasks, useUpdateTask, taskFieldChange } from '@/hooks/useTasks';
import { StatusBadge } from './StatusBadge';
import { PriorityBadge } from './PriorityBadge';
import { ApiError } from '@/api/httpClient';
import { formatDate } from '@/utils/formatDate';
// Reuses .dependencies-section__empty for the "no subtasks yet" message rather than redefining
// the same look under a new class name.
import './DependenciesSection.css';
import './SubtasksSection.css';

interface SubtasksSectionProps {
  task: Task;
  projectId: string;
  members: UserSummary[];
  onOpenDetail: (taskId: string) => void;
  canEdit: boolean;
}

export function SubtasksSection({ task, projectId, members, onOpenDetail, canEdit }: SubtasksSectionProps) {
  const { data: subtasks } = useSubtasks(task.id);
  const createSubtask = useCreateSubtask(projectId);
  const [showAddForm, setShowAddForm] = useState(false);

  const total = subtasks?.length ?? 0;
  const completed = subtasks?.filter((s) => s.status === 'Complete').length ?? 0;
  const percent = total > 0 ? Math.round((completed / total) * 100) : 0;

  return (
    <div className="task-detail-panel__section">
      <div className="subtasks-section__header">
        <h3>Subtasks</h3>
        {canEdit && (
          <button type="button" className="subtasks-section__add" onClick={() => setShowAddForm((v) => !v)}>
            {showAddForm ? 'Cancel' : '+ Add Subtask'}
          </button>
        )}
      </div>

      {total > 0 && (
        <div className="subtasks-section__progress">
          <span className="subtasks-section__progress-label">
            {completed} of {total} complete
          </span>
          <div className="subtasks-section__progress-bar">
            <div className="subtasks-section__progress-fill" style={{ width: `${percent}%` }} />
          </div>
          <span className="subtasks-section__progress-percent">{percent}%</span>
        </div>
      )}

      <div className="subtasks-section__list">
        {subtasks?.length === 0 && !showAddForm && <p className="dependencies-section__empty">No subtasks yet.</p>}
        {subtasks?.map((subtask) => (
          <SubtaskRow key={subtask.id} subtask={subtask} projectId={projectId} onOpen={() => onOpenDetail(subtask.id)} canEdit={canEdit} />
        ))}
      </div>

      {showAddForm && canEdit && (
        <AddSubtaskForm
          parentTaskId={task.id}
          members={members}
          isPending={createSubtask.isPending}
          errorMessage={
            createSubtask.isError
              ? createSubtask.error instanceof ApiError
                ? createSubtask.error.message
                : 'Could not create subtask.'
              : null
          }
          onCreate={(request) =>
            createSubtask.mutate({ parentTaskId: task.id, request }, { onSuccess: () => setShowAddForm(false) })
          }
          onCancel={() => setShowAddForm(false)}
        />
      )}
    </div>
  );
}

function SubtaskRow({
  subtask,
  projectId,
  onOpen,
  canEdit,
}: {
  subtask: Task;
  projectId: string;
  onOpen: () => void;
  canEdit: boolean;
}) {
  const updateTask = useUpdateTask(projectId);
  const isComplete = subtask.status === 'Complete';

  function toggleComplete(event: MouseEvent) {
    event.stopPropagation();
    updateTask.mutate({ task: subtask, change: taskFieldChange.status(isComplete ? 'NotStarted' : 'Complete') });
  }

  return (
    <div className="subtask-row" onClick={onOpen} role="button" tabIndex={0}>
      <button
        type="button"
        className={`subtask-row__checkbox${isComplete ? ' subtask-row__checkbox--checked' : ''}`}
        aria-label={isComplete ? 'Mark as not started' : 'Mark as complete'}
        onClick={toggleComplete}
        disabled={!canEdit || updateTask.isPending}
      >
        {isComplete && <Check size={12} />}
      </button>
      <div className="subtask-row__body">
        <span className={`subtask-row__title${isComplete ? ' subtask-row__title--complete' : ''}`}>{subtask.title}</span>
        <div className="subtask-row__meta">
          <StatusBadge status={subtask.status} />
          <PriorityBadge priority={subtask.priority} />
          {subtask.assignedTo && <span className="subtask-row__assignee">{subtask.assignedTo.name}</span>}
          {subtask.dueDate && <span className="subtask-row__due">Due {formatDate(subtask.dueDate)}</span>}
        </div>
      </div>
    </div>
  );
}

interface AddSubtaskFormProps {
  parentTaskId: string;
  members: UserSummary[];
  isPending: boolean;
  errorMessage: string | null;
  onCreate: (request: {
    title: string;
    description?: string;
    priority?: TaskPriority;
    assignedToUserId?: string;
    startDate?: string;
    dueDate?: string;
  }) => void;
  onCancel: () => void;
}

function AddSubtaskForm({ members, isPending, errorMessage, onCreate, onCancel }: AddSubtaskFormProps) {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [priority, setPriority] = useState<TaskPriority | ''>('');
  const [assigneeId, setAssigneeId] = useState('');
  const [startDate, setStartDate] = useState('');
  const [dueDate, setDueDate] = useState('');

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = title.trim();
    if (!trimmed) {
      return;
    }
    onCreate({
      title: trimmed,
      description: description.trim() || undefined,
      priority: priority || undefined,
      assignedToUserId: assigneeId || undefined,
      startDate: startDate || undefined,
      dueDate: dueDate || undefined,
    });
  }

  return (
    <form className="subtask-form" onSubmit={handleSubmit}>
      <input
        type="text"
        placeholder="Subtask title"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        autoFocus
        disabled={isPending}
      />
      <textarea
        placeholder="Description (optional)"
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        rows={2}
        disabled={isPending}
      />
      <div className="subtask-form__row">
        <select value={assigneeId} onChange={(e) => setAssigneeId(e.target.value)} disabled={isPending}>
          <option value="">Unassigned</option>
          {members.map((member) => (
            <option key={member.id} value={member.id}>
              {member.name}
            </option>
          ))}
        </select>
        <select value={priority} onChange={(e) => setPriority(e.target.value as TaskPriority)} disabled={isPending}>
          <option value="">Default priority</option>
          {PRIORITY_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      </div>
      <div className="subtask-form__row">
        <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} disabled={isPending} />
        <input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} disabled={isPending} />
      </div>

      {errorMessage && <p className="form-error">{errorMessage}</p>}

      <div className="subtask-form__actions">
        <button type="button" onClick={onCancel} disabled={isPending}>
          Cancel
        </button>
        <button type="submit" className="subtasks-section__add" disabled={isPending || !title.trim()}>
          {isPending ? 'Adding...' : 'Add Subtask'}
        </button>
      </div>
    </form>
  );
}
