import { useState, type FormEvent } from 'react';
import { Plus } from 'lucide-react';
import { useCreateProject } from '@/hooks/useProjects';
import { ApiError } from '@/api/httpClient';
import './NewUserForm.css';

// Same trigger-button-then-inline-form interaction as NewTaskButton/NewUserForm, reusing
// the existing useCreateProject mutation (the same one the sidebar's create form uses).
export function NewProjectForm() {
  const createProject = useCreateProject();
  const [isCreating, setIsCreating] = useState(false);
  const [name, setName] = useState('');

  function cancel() {
    setIsCreating(false);
    setName('');
    createProject.reset();
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = name.trim();
    if (!trimmed) {
      return;
    }

    createProject.mutate({ name: trimmed }, { onSuccess: cancel });
  }

  if (!isCreating) {
    return (
      <button type="button" className="new-user-button" onClick={() => setIsCreating(true)}>
        <Plus size={16} />
        <span>New Project</span>
      </button>
    );
  }

  return (
    <form className="new-user-form" onSubmit={handleSubmit}>
      <input
        type="text"
        placeholder="Project name"
        value={name}
        onChange={(e) => setName(e.target.value)}
        autoFocus
        disabled={createProject.isPending}
      />
      <button type="submit" disabled={createProject.isPending}>
        Create
      </button>
      <button type="button" className="new-user-form__cancel" onClick={cancel}>
        Cancel
      </button>
      {createProject.isError && (
        <span className="new-user-form__error">
          {createProject.error instanceof ApiError ? createProject.error.message : 'Could not create project.'}
        </span>
      )}
    </form>
  );
}
