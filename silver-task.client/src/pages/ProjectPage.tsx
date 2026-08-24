import { useState, type FormEvent } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { Trash2 } from 'lucide-react';
import {
  useAddProjectMember,
  useProject,
  useProjectMembers,
  useRemoveProjectMember,
  useUpdateProject,
} from '@/hooks/useProjects';
import { useDeleteTask, useDuplicateTask, useTasks } from '@/hooks/useTasks';
import { useTaskFilters } from '@/hooks/useTaskFilters';
import { useCustomFields } from '@/hooks/useCustomFields';
import { useCurrentUser } from '@/hooks/useAuth';
import { ApiError } from '@/api/httpClient';
import { ProjectViewTabs, type ViewId } from '@/components/project/ProjectViewTabs';
import { NewTaskButton } from '@/components/spreadsheet/NewTaskButton';
import { TaskTable } from '@/components/spreadsheet/TaskTable';
import { KanbanBoard } from '@/components/kanban/KanbanBoard';
import { CalendarView } from '@/components/calendar/CalendarView';
import { TaskSearchInput } from '@/components/spreadsheet/TaskSearchInput';
import { TaskFilterPanel } from '@/components/spreadsheet/TaskFilterPanel';
import { TaskSortMenu } from '@/components/spreadsheet/TaskSortMenu';
import { CustomFieldsPanel } from '@/components/spreadsheet/CustomFieldsPanel';
import { TaskDetailPanel } from '@/components/spreadsheet/TaskDetailPanel';
import { initials } from '@/utils/initials';
import './ProjectPage.css';

const TASK_QUERY_PARAM = 'task';
const VIEW_QUERY_PARAM = 'view';

export function ProjectPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const { data: project, isLoading, isError } = useProject(projectId);
  const { data: members } = useProjectMembers(projectId);
  const { data: tasks, isLoading: tasksLoading } = useTasks(projectId);
  const { data: customFields } = useCustomFields(projectId);
  const { data: currentUser } = useCurrentUser();
  const updateProject = useUpdateProject(projectId ?? '');
  const addMember = useAddProjectMember(projectId ?? '');
  const removeMember = useRemoveProjectMember(projectId ?? '');
  const duplicateTask = useDuplicateTask(projectId ?? '');
  const deleteTask = useDeleteTask(projectId ?? '');
  const memberUsers = members?.map((m) => m.user) ?? [];
  const {
    filteredTasks,
    isFiltered,
    searchQuery,
    setSearchQuery,
    filters,
    setFilters,
    clearFilters,
    activeFilterCount,
    sortField,
    sortDirection,
    setSortField,
    setSortDirection,
  } = useTaskFilters(tasks ?? [], customFields ?? []);

  const [isEditingName, setIsEditingName] = useState(false);
  const [nameDraft, setNameDraft] = useState('');
  const [isEditingDescription, setIsEditingDescription] = useState(false);
  const [descriptionDraft, setDescriptionDraft] = useState('');
  const [memberEmail, setMemberEmail] = useState('');

  const selectedTaskId = searchParams.get(TASK_QUERY_PARAM);
  const selectedTask = tasks?.find((t) => t.id === selectedTaskId) ?? null;

  // URL-driven like the task detail panel's `?task=` param — makes the current view linkable
  // and back-button-navigable. Omitted from the URL entirely when it's the default ("table").
  const view = (searchParams.get(VIEW_QUERY_PARAM) as ViewId | null) ?? 'table';

  function setView(next: ViewId) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev);
      if (next === 'table') {
        params.delete(VIEW_QUERY_PARAM);
      } else {
        params.set(VIEW_QUERY_PARAM, next);
      }
      return params;
    });
  }

  function openTaskDetail(taskId: string) {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set(TASK_QUERY_PARAM, taskId);
      return next;
    });
  }

  function closeTaskDetail() {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.delete(TASK_QUERY_PARAM);
      return next;
    });
  }

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
      </div>

      <div className="project-toolbar">
        <ProjectViewTabs active={view} onChange={setView} />
        <div className="project-toolbar__actions">
          <TaskSearchInput value={searchQuery} onChange={setSearchQuery} />
          <TaskFilterPanel
            filters={filters}
            onChange={setFilters}
            onClear={clearFilters}
            activeCount={activeFilterCount}
            members={memberUsers}
          />
          <TaskSortMenu
            sortField={sortField}
            sortDirection={sortDirection}
            onFieldChange={setSortField}
            onDirectionChange={setSortDirection}
          />
          <CustomFieldsPanel projectId={project.id} />
          <NewTaskButton projectId={project.id} />
        </div>
      </div>

      {tasksLoading ? (
        <p>Loading tasks...</p>
      ) : view === 'kanban' ? (
        <KanbanBoard projectId={project.id} tasks={filteredTasks} onOpenDetail={openTaskDetail} />
      ) : view === 'calendar' ? (
        <CalendarView projectId={project.id} tasks={filteredTasks} onOpenDetail={openTaskDetail} />
      ) : (
        <TaskTable
          projectId={project.id}
          tasks={filteredTasks}
          members={memberUsers}
          customFields={customFields ?? []}
          isFiltered={isFiltered}
          sortField={sortField}
          sortDirection={sortDirection}
          onSortFieldClick={setSortField}
          onDuplicate={(taskId) => duplicateTask.mutate(taskId)}
          onDelete={(taskId) => deleteTask.mutate(taskId)}
          onOpenDetail={openTaskDetail}
        />
      )}

      {selectedTask && (
        <TaskDetailPanel
          task={selectedTask}
          projectId={project.id}
          members={memberUsers}
          customFields={customFields ?? []}
          currentUserId={currentUser?.id}
          onClose={closeTaskDetail}
        />
      )}

      <details className="project-page__section">
        <summary>Members ({members?.length ?? 0})</summary>

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
      </details>
    </div>
  );
}
