import type { Task } from '@/types/task';
import { useDeleteTask } from '@/hooks/useTasks';
import { Modal } from '@/components/shared/Modal';
import '@/components/shared/ConfirmDeleteDialog.css';
import './DeleteTaskDialog.css';

interface DeleteTaskDialogProps {
  task: Task;
  projectId: string;
  onClose: () => void;
}

// A task with no subtasks deletes with a single plain confirmation, matching this app's existing
// low-friction delete pattern. A task *with* subtasks gets the spec's recommended three-way
// choice instead — "delete task only" (reparent children to this task's own parent, the safe
// default) vs "delete task + all subtasks" (the whole subtree, transactionally, on the backend)
// vs cancel — since silently picking one behavior for the user isn't appropriate once real data
// underneath it is at stake.
export function DeleteTaskDialog({ task, projectId, onClose }: DeleteTaskDialogProps) {
  const deleteTask = useDeleteTask(projectId);

  function handleDeleteOnly() {
    deleteTask.mutate({ taskId: task.id }, { onSuccess: onClose });
  }

  function handleDeleteWithSubtasks() {
    deleteTask.mutate({ taskId: task.id, deleteSubtasks: true }, { onSuccess: onClose });
  }

  if (task.subtaskCount === 0) {
    return (
      <Modal onClose={onClose}>
        <h2>Delete &ldquo;{task.title}&rdquo;?</h2>
        <p className="delete-task-dialog__message">This cannot be undone.</p>
        <div className="delete-task-dialog__actions">
          <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose} disabled={deleteTask.isPending}>
            Cancel
          </button>
          <button type="button" className="confirm-delete-dialog__delete" onClick={handleDeleteOnly} disabled={deleteTask.isPending}>
            {deleteTask.isPending ? 'Deleting...' : 'Delete'}
          </button>
        </div>
      </Modal>
    );
  }

  return (
    <Modal onClose={onClose}>
      <h2>Delete &ldquo;{task.title}&rdquo;?</h2>
      <p className="delete-task-dialog__message">
        This task contains {task.subtaskCount} subtask{task.subtaskCount === 1 ? '' : 's'}.
      </p>
      <div className="delete-task-dialog__actions delete-task-dialog__actions--stacked">
        <button
          type="button"
          className="delete-task-dialog__option"
          onClick={handleDeleteOnly}
          disabled={deleteTask.isPending}
        >
          Delete Task Only
          <span>Subtasks move up to this task&rsquo;s own parent.</span>
        </button>
        <button
          type="button"
          className="delete-task-dialog__option delete-task-dialog__option--danger"
          onClick={handleDeleteWithSubtasks}
          disabled={deleteTask.isPending}
        >
          Delete Task + All Subtasks
          <span>Permanently deletes this task and everything under it.</span>
        </button>
        <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose} disabled={deleteTask.isPending}>
          Cancel
        </button>
      </div>
    </Modal>
  );
}
