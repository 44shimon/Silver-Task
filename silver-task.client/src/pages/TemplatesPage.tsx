import { useMemo, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Copy, Download, LayoutTemplate, ListChecks, Plus, Share2, Star, Trash2 } from 'lucide-react';
import {
  useDeleteProjectTemplate,
  useDeleteTaskTemplate,
  useDuplicateProjectTemplate,
  useDuplicateTaskTemplate,
  useInstantiateTaskFromTemplate,
  useSetProjectTemplateArchived,
  useSetTaskTemplateArchived,
  useShareProjectTemplate,
  useShareTaskTemplate,
  useTemplatesList,
  useToggleProjectTemplateFavorite,
  useToggleTaskTemplateFavorite,
} from '@/hooks/useTemplates';
import { useProjects } from '@/hooks/useProjects';
import { usePermissions } from '@/hooks/usePermissions';
import { Permissions } from '@/types/permissions';
import { projectTemplatesApi } from '@/api/templatesApi';
import type { TemplateSummary } from '@/types/templates';
import { ApiError } from '@/api/httpClient';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import { formatDateTime } from '@/utils/formatDate';
import './TemplatesPage.css';

type FilterKey = 'all' | 'mine' | 'favorites' | 'recent' | 'archived';

const FILTERS: { key: FilterKey; label: string }[] = [
  { key: 'all', label: 'All' },
  { key: 'mine', label: 'My Templates' },
  { key: 'favorites', label: 'Favorites' },
  { key: 'recent', label: 'Recently Used' },
  { key: 'archived', label: 'Archived' },
];

// Template Home (spec's own "Template Center") — a single flat list mixing Project Templates and
// Task Templates (matching GET /api/templates, the unified read TemplatesController exposes),
// with per-row actions dispatched to the correct per-type endpoint (projectTemplatesApi vs.
// taskTemplatesApi) based on TemplateSummary.type. Filters are limited to what TemplateSummaryDto
// actually exposes (isOwnedByMe/isFavorite/lastUsedAt/isArchived) — no "Shared"/"Public" tab,
// since the list DTO doesn't carry per-row visibility (a disclosed scope simplification; the
// detail view still shows SharedWith/IsPublic for a template you own).
export function TemplatesPage() {
  const navigate = useNavigate();
  const { can } = usePermissions();
  const templates = useTemplatesList();
  const [filter, setFilter] = useState<FilterKey>('all');
  const [search, setSearch] = useState('');
  const [sharingId, setSharingId] = useState<string | null>(null);
  const [usingTaskTemplate, setUsingTaskTemplate] = useState<TemplateSummary | null>(null);

  const filtered = useMemo(() => {
    let list = templates.data ?? [];
    if (filter === 'mine') list = list.filter((t) => t.isOwnedByMe);
    else if (filter === 'favorites') list = list.filter((t) => t.isFavorite);
    else if (filter === 'recent') list = list.filter((t) => t.lastUsedAt);
    else if (filter === 'archived') list = list.filter((t) => t.isArchived);
    else list = list.filter((t) => !t.isArchived);

    const trimmed = search.trim().toLowerCase();
    if (trimmed) {
      list = list.filter(
        (t) =>
          t.name.toLowerCase().includes(trimmed) ||
          (t.description?.toLowerCase().includes(trimmed) ?? false) ||
          t.createdByName.toLowerCase().includes(trimmed),
      );
    }

    if (filter === 'recent') {
      return [...list].sort((a, b) => (b.lastUsedAt ?? '').localeCompare(a.lastUsedAt ?? ''));
    }
    return list;
  }, [templates.data, filter, search]);

  return (
    <div className="templates-page">
      <div className="templates-page__header">
        <h1>
          <LayoutTemplate size={20} />
          Templates
        </h1>
        {can(Permissions.TemplatesCreate) && (
          <div className="templates-page__create-actions">
            <button type="button" onClick={() => navigate('/templates/project/new')}>
              <Plus size={14} /> New Project Template
            </button>
            <button type="button" onClick={() => navigate('/templates/task/new')}>
              <Plus size={14} /> New Task Template
            </button>
          </div>
        )}
      </div>

      <div className="templates-page__toolbar">
        <input
          type="search"
          placeholder="Search templates by name, description, or creator..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="templates-page__search"
        />
        <div className="templates-page__filters">
          {FILTERS.map((f) => (
            <button
              key={f.key}
              type="button"
              className={`templates-page__filter${filter === f.key ? ' templates-page__filter--active' : ''}`}
              onClick={() => setFilter(f.key)}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      <DashboardWidget
        title="Templates"
        isLoading={templates.isLoading}
        isError={templates.isError}
        onRetry={() => templates.refetch()}
        isEmpty={filtered.length === 0}
        emptyTitle="No templates found"
        emptyMessage={
          filter === 'all' ? 'Create a Project Template or Task Template to get started.' : 'Try a different filter or search.'
        }
      >
        <ul className="templates-page__list">
          {filtered.map((template) => (
            <TemplateRow
              key={template.id}
              template={template}
              sharing={sharingId === template.id}
              onToggleSharing={() => setSharingId(sharingId === template.id ? null : template.id)}
              onUse={() => (template.type === 'Project' ? navigate(`/templates/new-project?templateId=${template.id}`) : setUsingTaskTemplate(template))}
            />
          ))}
        </ul>
      </DashboardWidget>

      {usingTaskTemplate && <UseTaskTemplateDialog template={usingTaskTemplate} onClose={() => setUsingTaskTemplate(null)} />}
    </div>
  );
}

interface TemplateRowProps {
  template: TemplateSummary;
  sharing: boolean;
  onToggleSharing: () => void;
  onUse: () => void;
}

function TemplateRow({ template, sharing, onToggleSharing, onUse }: TemplateRowProps) {
  const navigate = useNavigate();
  const { can } = usePermissions();
  const isAdmin = can(Permissions.AdministrationAccess);
  const canModify = template.isOwnedByMe || isAdmin;

  const toggleProjectFavorite = useToggleProjectTemplateFavorite();
  const toggleTaskFavorite = useToggleTaskTemplateFavorite();
  const duplicateProject = useDuplicateProjectTemplate();
  const duplicateTask = useDuplicateTaskTemplate();
  const archiveProject = useSetProjectTemplateArchived();
  const archiveTask = useSetTaskTemplateArchived();
  const deleteProject = useDeleteProjectTemplate();
  const deleteTask = useDeleteTaskTemplate();
  const shareProject = useShareProjectTemplate();
  const shareTask = useShareTaskTemplate();
  const [shareEmail, setShareEmail] = useState('');
  const [shareError, setShareError] = useState<string | null>(null);

  const isProject = template.type === 'Project';

  function toggleFavorite() {
    const favorite = !template.isFavorite;
    if (isProject) toggleProjectFavorite.mutate({ id: template.id, favorite });
    else toggleTaskFavorite.mutate({ id: template.id, favorite });
  }

  function duplicate() {
    if (isProject) duplicateProject.mutate(template.id);
    else duplicateTask.mutate(template.id);
  }

  function toggleArchived() {
    const archived = !template.isArchived;
    if (isProject) archiveProject.mutate({ id: template.id, archived });
    else archiveTask.mutate({ id: template.id, archived });
  }

  function remove() {
    if (!window.confirm(`Delete "${template.name}"? Projects and tasks already created from this template will not be affected. This cannot be undone.`)) {
      return;
    }
    if (isProject) deleteProject.mutate(template.id);
    else deleteTask.mutate(template.id);
  }

  function handleShare(event: FormEvent) {
    event.preventDefault();
    const email = shareEmail.trim();
    if (!email) return;
    setShareError(null);
    const mutation = isProject ? shareProject : shareTask;
    mutation.mutate(
      { id: template.id, request: { email } },
      {
        onSuccess: () => setShareEmail(''),
        onError: (err) => setShareError(err instanceof ApiError ? err.message : 'Could not share template.'),
      },
    );
  }

  function edit() {
    navigate(isProject ? `/templates/project/${template.id}` : `/templates/task/${template.id}`);
  }

  return (
    <li className={`templates-page__item${template.isArchived ? ' templates-page__item--archived' : ''}`}>
      <div className="templates-page__item-main">
        <button
          type="button"
          className="templates-page__favorite"
          aria-label={template.isFavorite ? 'Unfavorite' : 'Favorite'}
          onClick={toggleFavorite}
        >
          <Star size={14} fill={template.isFavorite ? 'currentColor' : 'none'} />
        </button>
        <span className={`templates-page__type-badge templates-page__type-badge--${template.type.toLowerCase()}`}>
          {template.type === 'Project' ? 'Project' : 'Task'}
        </span>
        <button type="button" className="templates-page__name" onClick={edit}>
          {template.name}
        </button>
        {template.isArchived && <span className="templates-page__archived-badge">Archived</span>}
        {!template.isOwnedByMe && <span className="templates-page__owner">by {template.createdByName}</span>}
      </div>

      {template.description && <p className="templates-page__description">{template.description}</p>}

      <div className="templates-page__meta">
        <span>
          <ListChecks size={12} /> {template.taskCount} task{template.taskCount === 1 ? '' : 's'}
        </span>
        <span>Used {template.usageCount} time{template.usageCount === 1 ? '' : 's'}</span>
        {template.lastUsedAt && <span>Last used {formatDateTime(template.lastUsedAt)}</span>}
      </div>

      <div className="templates-page__actions">
        {can(Permissions.TemplatesUse) && !template.isArchived && (
          <button type="button" onClick={onUse}>
            Use Template
          </button>
        )}
        {can(Permissions.TemplatesCreate) && (
          <button type="button" onClick={duplicate}>
            <Copy size={12} /> Duplicate
          </button>
        )}
        {isProject && (
          <a href={projectTemplatesApi.exportUrl(template.id)} download>
            <Download size={12} /> Export
          </a>
        )}
        {canModify && can(Permissions.TemplatesShare) && (
          <button type="button" onClick={onToggleSharing}>
            <Share2 size={12} /> Share
          </button>
        )}
        {canModify && (
          <button type="button" onClick={toggleArchived}>
            {template.isArchived ? 'Restore' : 'Archive'}
          </button>
        )}
        {canModify && (
          <button type="button" className="templates-page__delete" onClick={remove}>
            <Trash2 size={12} /> Delete
          </button>
        )}
      </div>

      {sharing && (
        <form className="templates-page__share-form" onSubmit={handleShare}>
          <input
            type="email"
            placeholder="user@example.com"
            value={shareEmail}
            onChange={(e) => setShareEmail(e.target.value)}
            required
          />
          <button type="submit" disabled={(isProject ? shareProject : shareTask).isPending}>
            Share
          </button>
          {shareError && <p className="templates-page__error">{shareError}</p>}
        </form>
      )}
    </li>
  );
}

function UseTaskTemplateDialog({ template, onClose }: { template: TemplateSummary; onClose: () => void }) {
  const { data: projects } = useProjects();
  const instantiate = useInstantiateTaskFromTemplate();
  const [projectId, setProjectId] = useState('');
  const [startDate, setStartDate] = useState('');
  const [error, setError] = useState<string | null>(null);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!projectId) return;
    setError(null);
    instantiate.mutate(
      { templateId: template.id, projectId, startDateOverride: startDate || undefined },
      {
        onSuccess: () => onClose(),
        onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not create task from template.'),
      },
    );
  }

  return (
    <div className="templates-page__modal-backdrop" onClick={onClose}>
      <div className="templates-page__modal" onClick={(e) => e.stopPropagation()}>
        <h2>Use "{template.name}"</h2>
        <form onSubmit={handleSubmit}>
          <label>
            Project
            <select value={projectId} onChange={(e) => setProjectId(e.target.value)} required autoFocus>
              <option value="" disabled>
                Select a project...
              </option>
              {projects?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Anchor date (optional)
            <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
          </label>
          {error && <p className="templates-page__error">{error}</p>}
          <div className="templates-page__modal-actions">
            <button type="button" onClick={onClose}>
              Cancel
            </button>
            <button type="submit" disabled={!projectId || instantiate.isPending}>
              Create Task
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
