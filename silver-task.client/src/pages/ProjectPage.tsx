import { useState, type FormEvent } from 'react';
import { useParams } from 'react-router-dom';
import { Trash2 } from 'lucide-react';
import {
  useAddProjectMember,
  useProject,
  useProjectMembers,
  useRemoveProjectMember,
  useUpdateProject,
} from '@/hooks/useProjects';
import { ApiError } from '@/api/httpClient';
import './ProjectPage.css';

export function ProjectPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const { data: project, isLoading, isError } = useProject(projectId);
  const { data: members } = useProjectMembers(projectId);
  const updateProject = useUpdateProject(projectId ?? '');
  const addMember = useAddProjectMember(projectId ?? '');
  const removeMember = useRemoveProjectMember(projectId ?? '');

  const [isEditingName, setIsEditingName] = useState(false);
  const [nameDraft, setNameDraft] = useState('');
  const [isEditingDescription, setIsEditingDescription] = useState(false);
  const [descriptionDraft, setDescriptionDraft] = useState('');
  const [memberEmail, setMemberEmail] = useState('');

  if (isLoading) {
    return <p>Loading...</p>;
  }

  if (isError || !project) {
    return <p>This project could not be loaded. You may not have access to it.</p>;
  }

  function startEditingName() {
    setNameDraft(project!.name);
    setIsEditingName(true);
  }

  function commitName() {
    const trimmed = nameDraft.trim();
    if (trimmed && trimmed !== project!.name) {
      updateProject.mutate({ name: trimmed, description: project!.description ?? undefined });
    }
    setIsEditingName(false);
  }

  function startEditingDescription() {
    setDescriptionDraft(project!.description ?? '');
    setIsEditingDescription(true);
  }

  function commitDescription() {
    const trimmed = descriptionDraft.trim();
    if (trimmed !== (project!.description ?? '')) {
      updateProject.mutate({ name: project!.name, description: trimmed || undefined });
    }
    setIsEditingDescription(false);
  }

  function handleAddMember(event: FormEvent) {
    event.preventDefault();
    const trimmed = memberEmail.trim();
    if (!trimmed) {
      return;
    }

    addMember.mutate(
      { email: trimmed },
      {
        onSuccess: () => setMemberEmail(''),
      },
    );
  }

  return (
    <div className="project-page">
      <div className="project-page__header">
        <div className="project-page__title-row">
          {isEditingName ? (
            <input
              type="text"
              value={nameDraft}
              onChange={(e) => setNameDraft(e.target.value)}
              onBlur={commitName}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.currentTarget.blur();
                }
                if (e.key === 'Escape') {
                  setIsEditingName(false);
                }
              }}
              autoFocus
            />
          ) : (
            <h1 onClick={startEditingName} title="Click to rename">
              {project.name}
            </h1>
          )}
        </div>

        {isEditingDescription ? (
          <textarea
            value={descriptionDraft}
            onChange={(e) => setDescriptionDraft(e.target.value)}
            onBlur={commitDescription}
            onKeyDown={(e) => {
              if (e.key === 'Escape') {
                setIsEditingDescription(false);
              }
            }}
            placeholder="Add a description..."
            autoFocus
          />
        ) : (
          <p className="project-page__description" onClick={startEditingDescription} title="Click to edit">
            {project.description || 'Add a description...'}
          </p>
        )}

        <div className="project-page__meta">
          <span>Owner: {project.owner.name}</span>
          <span>Created {new Date(project.createdAt).toLocaleDateString()}</span>
        </div>
      </div>

      <div className="project-page__section">
        <h2>Members</h2>

        <div className="member-list">
          {members?.map((member) => (
            <div className="member-row" key={member.id}>
              <div className="member-row__avatar">{initials(member.user.name)}</div>
              <div className="member-row__info">
                <span className="member-row__name">{member.user.name}</span>
                <span className="member-row__email">{member.user.email}</span>
              </div>
              {member.user.id === project.owner.id ? (
                <span className="member-row__owner-badge">Owner</span>
              ) : (
                <button
                  className="icon-button member-row__remove"
                  type="button"
                  aria-label={`Remove ${member.user.name}`}
                  onClick={() => removeMember.mutate(member.user.id)}
                >
                  <Trash2 size={16} />
                </button>
              )}
            </div>
          ))}
        </div>

        <form className="add-member-form" onSubmit={handleAddMember}>
          <input
            type="email"
            placeholder="Add member by email"
            value={memberEmail}
            onChange={(e) => setMemberEmail(e.target.value)}
            disabled={addMember.isPending}
          />
          <button type="submit" disabled={addMember.isPending}>
            Add
          </button>
        </form>
        {addMember.isError && (
          <p className="form-error">
            {addMember.error instanceof ApiError ? addMember.error.message : 'Could not add member.'}
          </p>
        )}
      </div>
    </div>
  );
}

function initials(name: string): string {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('');
}
