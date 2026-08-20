import { useState, type FormEvent } from 'react';
import { Plus } from 'lucide-react';
import { useCreateTask } from '@/hooks/useTasks';
import { ApiError } from '@/api/httpClient';
import './NewTaskButton.css';

interface NewTaskButtonProps {
  projectId: string;
}

export function NewTaskButton({ projectId }: NewTaskButtonProps) {
  const createTask = useCreateTask(projectId);
  const [isCreating, setIsCreating] = useState(false);
  const [title, setTitle] = useState('');

  function cancel() {
    setIsCreating(false);
    setTitle('');
    createTask.reset();
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = title.trim();
    if (!trimmed) {
      return;
    }

    // Deliberately doesn't close the form on success, so adding several tasks
    // in a row (the common case) doesn't require re-opening it each time.
    createTask.mutate(
      { title: trimmed },
      {
        onSuccess: () => setTitle(''),
      },
    );
  }

  if (!isCreating) {
    return (
      <button type="button" className="new-task-button" onClick={() => setIsCreating(true)}>
        <Plus size={16} />
        <span>New Task</span>
      </button>
    );
  }

  return (
    <form className="new-task-form" onSubmit={handleSubmit}>
      <input
        type="text"
        placeholder="Task title"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Escape') {
            cancel();
          }
        }}
        autoFocus
        disabled={createTask.isPending}
      />
      <button type="submit" disabled={createTask.isPending}>
        Add
      </button>
      <button type="button" className="new-task-form__cancel" onClick={cancel}>
        Cancel
      </button>
      {createTask.isError && (
        <span className="new-task-form__error">
          {createTask.error instanceof ApiError ? createTask.error.message : 'Could not create task.'}
        </span>
      )}
    </form>
  );
}
