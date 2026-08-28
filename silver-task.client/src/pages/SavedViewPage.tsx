import { useEffect, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { Copy, Share2, Star, Trash2 } from 'lucide-react';
import { useCurrentUser } from '@/hooks/useAuth';
import { useCustomFields } from '@/hooks/useCustomFields';
import { useProject, useProjectMembers, useProjects } from '@/hooks/useProjects';
import { useTasks } from '@/hooks/useTasks';
import { useProjectPermissions } from '@/hooks/usePermissions';
import { Permissions } from '@/types/permissions';
import { useDebouncedValue } from '@/hooks/useDebouncedValue';
import {
  useCreateSavedView,
  useDeleteSavedView,
  useDuplicateSavedView,
  useExecuteSavedView,
  usePreviewSavedView,
  useSavedView,
  useShareSavedView,
  useToggleSavedViewFavorite,
  useUnshareSavedView,
  useUpdateSavedView,
} from '@/hooks/useSavedViews';
import { FilterBuilder } from '@/components/filters/FilterBuilder';
import { MyTasksTable } from '@/components/dashboard/MyTasksTable';
import { TaskDetailPanel } from '@/components/spreadsheet/TaskDetailPanel';
import type { MyTaskSortField } from '@/hooks/useMyTasksFilters';
import type { SortDirection } from '@/utils/taskFilters';
import { emptyFilterGroup, SAVED_VIEW_LAYOUTS, type SavedViewEntityType, type SavedViewFilterGroup, type SavedViewLayout, type SaveViewRequest } from '@/types/savedView';
import './SavedViewPage.css';

const TASK_QUERY_PARAM = 'task';
const PAGE_SIZE = 50;

interface Draft {
  name: string;
  description: string;
  entityType: SavedViewEntityType;
  isPublic: boolean;
  filter: SavedViewFilterGroup;
  sortField: string | null;
  sortDescending: boolean;
  layout: SavedViewLayout;
}

function draftFromRequest(view: { name: string; description: string | null; entityType: SavedViewEntityType; isPublic: boolean; filter: SavedViewFilterGroup; sortField: string | null; sortDescending: boolean; layout: SavedViewLayout }): Draft {
  return {
    name: view.name,
    description: view.description ?? '',
    entityType: view.entityType,
    isPublic: view.isPublic,
    filter: view.filter,
    sortField: view.sortField,
    sortDescending: view.sortDescending,
    layout: view.layout,
  };
}

const BLANK_DRAFT: Draft = {
  name: '',
  description: '',
  entityType: 'Task',
  isPublic: false,
  filter: emptyFilterGroup(),
  sortField: 'dueDate',
  sortDescending: false,
  layout: 'Table',
};

/**
 * Phase 43 — the Saved View editor + results page, at `/views/{id}` (a stable, linkable URL per
 * the spec's own explicit requirement) or `/views/new` for the Create View flow. Reuses
 * MyTasksTable for rendering results (the app's only existing cross-project task table — see its
 * own doc comment) and TaskDetailPanel via the same `?task=` URL-param convention MyTasksPage/
 * ProjectPage already use, rather than inventing a second results grid or detail panel.
 *
 * Filter/column/sort/layout edits are local ("draft") state until Save Changes is clicked — the
 * results table always renders the last SAVED version, never the in-progress draft, so a filter
 * edit can never silently overwrite what's actually persisted (spec's own explicit rule). The
 * lightweight "N matching" count next to the filter builder is the one place the draft is
 * evaluated live, via a debounced preview call.
 */
export function SavedViewPage() {
  const { id } = useParams<{ id: string }>();
  const isNew = id === 'new' || id === undefined;
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [page, setPage] = useState(1);

  const { data: view, isLoading } = useSavedView(isNew ? undefined : id);
  const { data: currentUser } = useCurrentUser();
  const { data: allProjects } = useProjects();

  const [draft, setDraft] = useState<Draft>(BLANK_DRAFT);
  const [referenceProjectId, setReferenceProjectId] = useState<string | null>(null);
  const [savedSnapshot, setSavedSnapshot] = useState<Draft>(BLANK_DRAFT);
  const [shareEmail, setShareEmail] = useState('');

  useEffect(() => {
    if (view) {
      const next = draftFromRequest(view);
      setDraft(next);
      setSavedSnapshot(next);
    }
  }, [view]);

  const create = useCreateSavedView();
  const update = useUpdateSavedView(id ?? '');
  const remove = useDeleteSavedView();
  const duplicate = useDuplicateSavedView();
  const toggleFavorite = useToggleSavedViewFavorite();
  const share = useShareSavedView();
  const unshare = useUnshareSavedView();

  const isDirty = !isNew && JSON.stringify(draft) !== JSON.stringify(savedSnapshot);

  const debouncedFilter = useDebouncedValue(draft.filter, 400);
  const { data: preview } = usePreviewSavedView(
    draft.name.trim().length > 0 ? { entityType: draft.entityType, filter: debouncedFilter } : null,
  );

  const { data: results, isLoading: isExecuting } = useExecuteSavedView(isNew ? undefined : id, page, PAGE_SIZE);

  const selectedTaskId = searchParams.get(TASK_QUERY_PARAM);
  const selectedTask = results?.tasks.find((t) => t.id === selectedTaskId) ?? null;
  const { data: selectedTaskMembers } = useProjectMembers(selectedTask?.projectId);
  const { data: selectedTaskCustomFields } = useCustomFields(selectedTask?.projectId);
  const { data: selectedProjectTasks } = useTasks(selectedTask?.projectId);
  const { data: selectedTaskProject } = useProject(selectedTask?.projectId);
  const { can } = useProjectPermissions(selectedTaskProject);

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

  function buildRequest(): SaveViewRequest {
    return {
      name: draft.name.trim(),
      description: draft.description.trim() || null,
      entityType: draft.entityType,
      isPublic: draft.isPublic,
      filter: draft.filter,
      sortField: draft.sortField,
      sortDescending: draft.sortDescending,
      layout: draft.layout,
    };
  }

  async function handleSave() {
    if (!draft.name.trim()) {
      return;
    }
    if (isNew) {
      const created = await create.mutateAsync(buildRequest());
      navigate(`/views/${created.id}`, { replace: true });
    } else {
      const saved = await update.mutateAsync(buildRequest());
      setSavedSnapshot(draftFromRequest(saved));
    }
  }

  function handleDiscard() {
    setDraft(savedSnapshot);
  }

  async function handleSaveAsNew() {
    const created = await create.mutateAsync({ ...buildRequest(), name: `${draft.name.trim()} (Copy)` });
    navigate(`/views/${created.id}`);
  }

  async function handleDelete() {
    if (!id || isNew) return;
    if (window.confirm('Delete this saved view? This will not delete any tasks or projects.')) {
      await remove.mutateAsync(id);
      navigate('/views');
    }
  }

  async function handleDuplicate() {
    if (!id || isNew) return;
    const copy = await duplicate.mutateAsync(id);
    navigate(`/views/${copy.id}`);
  }

  function handleSortClick(field: MyTaskSortField) {
    setDraft((d) => (d.sortField === field ? { ...d, sortDescending: !d.sortDescending } : { ...d, sortField: field, sortDescending: false }));
  }

  if (!isNew && isLoading) {
    return <p>Loading view...</p>;
  }
  if (!isNew && !view) {
    return <p>This view could not be found or you don't have access to it.</p>;
  }

  const isSystemDefault = view?.isSystemDefault ?? false;
  const canEditMeta = !isSystemDefault && (isNew || view?.isOwnedByMe);
  const totalPages = results ? Math.max(1, Math.ceil(results.total / PAGE_SIZE)) : 1;

  return (
    <div className="saved-view-page">
      <div className="saved-view-page__header">
        {canEditMeta ? (
          <input
            className="saved-view-page__name-input"
            value={draft.name}
            placeholder="View name"
            onChange={(e) => setDraft((d) => ({ ...d, name: e.target.value }))}
          />
        ) : (
          <h1>{draft.name || 'Untitled View'}</h1>
        )}

        <div className="saved-view-page__header-actions">
          {!isNew && !isSystemDefault && (
            <button
              type="button"
              className="icon-button"
              aria-label={view?.isFavorite ? 'Remove from favorites' : 'Add to favorites'}
              onClick={() => id && toggleFavorite.mutate({ id, favorite: !view?.isFavorite })}
            >
              <Star size={16} fill={view?.isFavorite ? 'currentColor' : 'none'} />
            </button>
          )}
          {!isNew && (
            <button type="button" className="icon-button" aria-label="Duplicate view" onClick={handleDuplicate}>
              <Copy size={16} />
            </button>
          )}
          {!isNew && !isSystemDefault && view?.isOwnedByMe && (
            <button type="button" className="icon-button" aria-label="Delete view" onClick={handleDelete}>
              <Trash2 size={16} />
            </button>
          )}
        </div>
      </div>

      {canEditMeta && (
        <textarea
          className="saved-view-page__description-input"
          placeholder="Description (optional)"
          value={draft.description}
          onChange={(e) => setDraft((d) => ({ ...d, description: e.target.value }))}
        />
      )}

      {canEditMeta && (
        <div className="saved-view-page__meta-row">
          {isNew && (
            <label>
              Based on
              <select value={draft.entityType} onChange={(e) => setDraft((d) => ({ ...d, entityType: e.target.value as SavedViewEntityType }))}>
                <option value="Task">Tasks</option>
                <option value="Project">Projects</option>
              </select>
            </label>
          )}
          <label>
            Visibility
            <select value={draft.isPublic ? 'public' : 'private'} onChange={(e) => setDraft((d) => ({ ...d, isPublic: e.target.value === 'public' }))}>
              <option value="private">Private</option>
              <option value="public">Public (everyone can view)</option>
            </select>
          </label>
          {draft.entityType === 'Task' && (
            <label>
              Layout
              <select value={draft.layout} onChange={(e) => setDraft((d) => ({ ...d, layout: e.target.value as SavedViewLayout }))}>
                {SAVED_VIEW_LAYOUTS.map((l) => (
                  <option key={l} value={l}>
                    {l}
                  </option>
                ))}
              </select>
            </label>
          )}
        </div>
      )}

      {canEditMeta && (
        <div className="saved-view-page__filters">
          <FilterBuilder
            group={draft.filter}
            onChange={(filter) => setDraft((d) => ({ ...d, filter }))}
            entityType={draft.entityType}
            referenceProjectId={referenceProjectId}
            onReferenceProjectChange={setReferenceProjectId}
          />
          {preview && (
            <p className="saved-view-page__preview-count">
              {preview.total} matching {draft.entityType === 'Task' ? 'task' : 'project'}
              {preview.total === 1 ? '' : 's'}
              {preview.unavailableFilterFields.length > 0 && (
                <span className="saved-view-page__unavailable"> · Filter unavailable: {preview.unavailableFilterFields.join(', ')}</span>
              )}
            </p>
          )}
        </div>
      )}

      {canEditMeta && (
        <div className="saved-view-page__save-bar">
          {isDirty && (
            <>
              <span>Unsaved changes</span>
              <button type="button" onClick={handleDiscard}>
                Discard
              </button>
              <button type="button" className="saved-view-page__save-button" onClick={handleSave}>
                Save Changes
              </button>
            </>
          )}
          {isNew && (
            <button type="button" className="saved-view-page__save-button" disabled={!draft.name.trim()} onClick={handleSave}>
              Save View
            </button>
          )}
          {!isNew && !isDirty && (
            <button type="button" onClick={handleSaveAsNew}>
              Save As New View
            </button>
          )}
        </div>
      )}

      {!isNew && view?.isOwnedByMe && !isSystemDefault && (
        <details className="saved-view-page__share">
          <summary>
            <Share2 size={13} /> Sharing {view.sharedWith && view.sharedWith.length > 0 && `(${view.sharedWith.length})`}
          </summary>
          <div className="saved-view-page__share-body">
            <div className="saved-view-page__share-form">
              <input type="email" placeholder="Share with email..." value={shareEmail} onChange={(e) => setShareEmail(e.target.value)} />
              <button
                type="button"
                onClick={async () => {
                  if (id && shareEmail.trim()) {
                    await share.mutateAsync({ id, email: shareEmail.trim() });
                    setShareEmail('');
                  }
                }}
              >
                Share
              </button>
            </div>
            {view.sharedWith?.map((u) => (
              <div key={u.userId} className="saved-view-page__share-row">
                <span>{u.name}</span>
                <button type="button" onClick={() => id && unshare.mutate({ id, userId: u.userId })}>
                  Remove
                </button>
              </div>
            ))}
          </div>
        </details>
      )}

      {!isNew && draft.entityType === 'Task' && (
        <>
          {isExecuting && <p>Loading results...</p>}
          {!isExecuting && results && (
            <MyTasksTable
              tasks={results.tasks}
              isFiltered
              sortField={(draft.sortField as MyTaskSortField) ?? 'dueDate'}
              sortDirection={(draft.sortDescending ? 'desc' : 'asc') as SortDirection}
              onSortFieldClick={handleSortClick}
              onOpenDetail={openTaskDetail}
            />
          )}
          {results && totalPages > 1 && (
            <div className="saved-view-page__pagination">
              <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                Previous
              </button>
              <span>
                Page {page} of {totalPages} ({results.total} total)
              </span>
              <button type="button" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                Next
              </button>
            </div>
          )}
        </>
      )}

      {!isNew && draft.entityType === 'Project' && results && (
        <ul className="saved-view-page__project-list">
          {results.projects.map((p) => (
            <li key={p.id}>
              <a href={`/projects/${p.id}`}>{p.name}</a>
            </li>
          ))}
          {results.projects.length === 0 && <p>No matching projects.</p>}
        </ul>
      )}

      {results?.resolvedSingleProjectId && allProjects?.some((p) => p.id === results.resolvedSingleProjectId) && (
        <p className="saved-view-page__single-project-hint">
          Every matching task belongs to one project —{' '}
          <a href={`/projects/${results.resolvedSingleProjectId}`}>open it directly for Kanban/Calendar/Timeline/Gantt</a>.
        </p>
      )}

      {selectedTask && (
        <TaskDetailPanel
          task={selectedTask}
          projectId={selectedTask.projectId}
          members={selectedTaskMembers?.map((m) => m.user) ?? []}
          customFields={selectedTaskCustomFields ?? []}
          tasks={selectedProjectTasks ?? []}
          currentUserId={currentUser?.id}
          onClose={closeTaskDetail}
          onOpenDetail={(taskId) => navigate(`/projects/${selectedTask.projectId}?task=${taskId}`)}
          canEdit={can(Permissions.TasksEdit)}
          canOverrideDependencies={can(Permissions.DependenciesOverride)}
        />
      )}
    </div>
  );
}
