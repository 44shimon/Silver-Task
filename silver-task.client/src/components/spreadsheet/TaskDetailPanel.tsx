import { useEffect, useState, type KeyboardEvent } from 'react';
import { X } from 'lucide-react';
import type { Task } from '@/types/task';
import type { CustomField } from '@/types/customField';
import type { UserSummary } from '@/types/project';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import { StatusDropdownCell } from './StatusDropdownCell';
import { PriorityDropdownCell } from './PriorityDropdownCell';
import { AssignedToDropdownCell } from './AssignedToDropdownCell';
import { EditableDateCell } from './EditableDateCell';
import { CustomFieldCell } from './CustomFieldCell';
import { CommentsSection } from './CommentsSection';
import { ActivityHistorySection } from './ActivityHistorySection';
import { AttachmentsSection } from './AttachmentsSection';
import { DependenciesSection } from './DependenciesSection';
import './TaskDetailPanel.css';

interface TaskDetailPanelProps {
  task: Task;
  projectId: string;
  members: UserSummary[];
  customFields: CustomField[];
  /** The full (unfiltered) project task list — passed through to DependenciesSection's Add
   * Dependency selector so it doesn't need its own fetch. */
  tasks: Task[];
  currentUserId: string | undefined;
  onClose: () => void;
  /** Swaps which task this already-open panel shows (updates the `?task=` param) — reused by
   * DependenciesSection so clicking a dependency opens it in the existing detail component. */
  onOpenDetail: (taskId: string) => void;
}

export function TaskDetailPanel({ task, projectId, members, customFields, tasks, currentUserId, onClose, onOpenDetail }: TaskDetailPanelProps) {
  useEffect(() => {
    function handleKeyDown(event: globalThis.KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose();
      }
    }
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  return (
    <div className="task-detail-backdrop" onClick={onClose}>
      <div className="task-detail-panel" onClick={(e) => e.stopPropagation()}>
        <div className="task-detail-panel__header">
          <TaskTitleField task={task} projectId={projectId} />
          <button type="button" className="icon-button" aria-label="Close task details" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div className="task-detail-panel__body">
          <div className="task-detail-panel__field">
            <span className="task-detail-panel__label">Description</span>
            <TaskDescriptionField task={task} projectId={projectId} />
          </div>

          <div className="task-detail-panel__row">
            <div className="task-detail-panel__field">
              <span className="task-detail-panel__label">Status</span>
              <StatusDropdownCell task={task} projectId={projectId} />
            </div>
            <div className="task-detail-panel__field">
              <span className="task-detail-panel__label">Priority</span>
              <PriorityDropdownCell task={task} projectId={projectId} />
            </div>
          </div>

          {/* Dependency-blocked is deliberately never written into Status itself (see
              TaskDependencyService) — shown here as its own separate line instead. */}
          {task.blockedByCount > 0 && (
            <p className="task-detail-panel__blocked-banner">
              Blocked by {task.blockedByCount} task{task.blockedByCount === 1 ? '' : 's'}
            </p>
          )}

          <div className="task-detail-panel__field">
            <span className="task-detail-panel__label">Assigned To</span>
            <AssignedToDropdownCell task={task} projectId={projectId} members={members} />
          </div>

          <div className="task-detail-panel__row">
            <div className="task-detail-panel__field">
              <span className="task-detail-panel__label">Start Date</span>
              <EditableDateCell task={task} projectId={projectId} field="startDate" />
            </div>
            <div className="task-detail-panel__field">
              <span className="task-detail-panel__label">Due Date</span>
              <EditableDateCell task={task} projectId={projectId} field="dueDate" />
            </div>
          </div>

          {customFields.length > 0 && (
            <div className="task-detail-panel__section">
              <h3>Custom Fields</h3>
              {customFields.map((field) => (
                <div className="task-detail-panel__field" key={field.id}>
                  <span className="task-detail-panel__label">{field.name}</span>
                  <CustomFieldCell task={task} field={field} projectId={projectId} members={members} />
                </div>
              ))}
            </div>
          )}

          <DependenciesSection task={task} projectId={projectId} tasks={tasks} onOpenDetail={onOpenDetail} />
          <AttachmentsSection taskId={task.id} />
          <CommentsSection taskId={task.id} currentUserId={currentUserId} />
          <ActivityHistorySection taskId={task.id} />
        </div>
      </div>
    </div>
  );
}

function TaskTitleField({ task, projectId }: { task: Task; projectId: string }) {
  const updateTask = useUpdateTask(projectId);
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState(task.title);

  function startEditing() {
    setDraft(task.title);
    setIsEditing(true);
  }

  function commit() {
    setIsEditing(false);
    const trimmed = draft.trim();
    if (trimmed && trimmed !== task.title) {
      updateTask.mutate({ task, change: taskFieldChange.title(trimmed) });
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.currentTarget.blur();
    } else if (event.key === 'Escape') {
      setIsEditing(false);
    }
  }

  if (isEditing) {
    return (
      <input
        className="task-detail-panel__title-input"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={handleKeyDown}
        autoFocus
      />
    );
  }

  return (
    <h2 className="task-detail-panel__title" onClick={startEditing} title="Click to rename">
      {task.title}
    </h2>
  );
}

function TaskDescriptionField({ task, projectId }: { task: Task; projectId: string }) {
  const updateTask = useUpdateTask(projectId);
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState('');

  function startEditing() {
    setDraft(task.description ?? '');
    setIsEditing(true);
  }

  function commit() {
    setIsEditing(false);
    const trimmed = draft.trim();
    if (trimmed !== (task.description ?? '')) {
      updateTask.mutate({ task, change: taskFieldChange.description(trimmed || null) });
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Escape') {
      setIsEditing(false);
    }
    // Enter intentionally doesn't commit here — it's a multiline field, so Enter should
    // insert a newline, matching the LongText custom field editor's behavior.
  }

  if (isEditing) {
    return (
      <textarea
        className="task-detail-panel__description-input"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={handleKeyDown}
        rows={4}
        autoFocus
      />
    );
  }

  return (
    <div className="task-detail-panel__description" onClick={startEditing} title="Click to edit">
      {task.description || <span className="editable-cell__placeholder">Add a description...</span>}
    </div>
  );
}
