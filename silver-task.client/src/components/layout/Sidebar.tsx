import { useState, type FormEvent } from 'react';
import { NavLink } from 'react-router-dom';
import { LayoutGrid, ListChecks, Plus } from 'lucide-react';
import { useCreateProject, useProjects } from '@/hooks/useProjects';
import { ApiError } from '@/api/httpClient';

export function Sidebar() {
  const { data: projects, isLoading } = useProjects();
  const createProject = useCreateProject();
  const [isCreating, setIsCreating] = useState(false);
  const [name, setName] = useState('');

  function cancelCreate() {
    setIsCreating(false);
    setName('');
    createProject.reset();
  }

  function handleCreate(event: FormEvent) {
    event.preventDefault();
    const trimmed = name.trim();
    if (!trimmed) {
      return;
    }

    createProject.mutate(
      { name: trimmed },
      {
        onSuccess: () => {
          setName('');
          setIsCreating(false);
        },
      },
    );
  }

  return (
    <aside className="sidebar">
      <nav className="sidebar__nav sidebar__nav--top">
        <NavLink
          to="/"
          end
          className={({ isActive }) => `sidebar__nav-item${isActive ? ' sidebar__nav-item--active' : ''}`}
        >
          <ListChecks size={16} />
          <span>My Tasks</span>
        </NavLink>
      </nav>

      <div className="sidebar__header">
        <span className="sidebar__section-title">Projects</span>
        <button
          className="icon-button"
          type="button"
          aria-label={isCreating ? 'Cancel new project' : 'New project'}
          onClick={() => (isCreating ? cancelCreate() : setIsCreating(true))}
        >
          <Plus size={16} />
        </button>
      </div>

      {isCreating && (
        <form className="sidebar__create-form" onSubmit={handleCreate}>
          <input
            type="text"
            placeholder="Project name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Escape') {
                cancelCreate();
              }
            }}
            autoFocus
            disabled={createProject.isPending}
          />
          {createProject.isError && (
            <p className="sidebar__create-error">
              {createProject.error instanceof ApiError ? createProject.error.message : 'Could not create project.'}
            </p>
          )}
        </form>
      )}

      {isLoading && <div className="sidebar__empty">Loading...</div>}

      {!isLoading && projects?.length === 0 && !isCreating && (
        <div className="sidebar__empty">
          <LayoutGrid size={16} />
          <span>No projects yet</span>
        </div>
      )}

      <nav className="sidebar__nav">
        {projects?.map((project) => (
          <NavLink
            key={project.id}
            to={`/projects/${project.id}`}
            className={({ isActive }) => `sidebar__nav-item${isActive ? ' sidebar__nav-item--active' : ''}`}
          >
            <LayoutGrid size={16} />
            <span>{project.name}</span>
          </NavLink>
        ))}
      </nav>
    </aside>
  );
}
