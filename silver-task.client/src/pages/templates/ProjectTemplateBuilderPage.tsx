import { useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, ChevronDown, ChevronRight, GripVertical, Plus, Trash2, X } from 'lucide-react';
import { PRIORITY_OPTIONS, STATUS_LABELS, STATUS_OPTIONS, type TaskPriority, type TaskStatus } from '@/types/task';
import { DEPENDENCY_TYPE_LABELS, DEPENDENCY_TYPE_OPTIONS, type DependencyType } from '@/types/dependency';
import {
  TEMPLATE_ASSIGNMENT_MODE_LABELS,
  TEMPLATE_ASSIGNMENT_MODE_OPTIONS,
  type SaveProjectTemplateDependencyRequest,
  type SaveProjectTemplateTaskRequest,
  type TemplateAssignmentMode,
} from '@/types/templates';
import { useCollaborators, useCreateProjectTemplate, useProjectTemplate, useUpdateProjectTemplate } from '@/hooks/useTemplates';
import { ApiError } from '@/api/httpClient';
import './TemplateBuilder.css';

interface DraftTask extends Omit<SaveProjectTemplateTaskRequest, 'sortOrder'> {
  /** Display label only — real title is `.title`; kept separate so a task with an empty
   * title-in-progress doesn't disappear from the list while being typed. */
  key: string;
}

function newDraftTask(): DraftTask {
  return {
    key: crypto.randomUUID(),
    clientId: crypto.randomUUID(),
    title: '',
    status: 'NotStarted',
    priority: 'Medium',
    assignmentMode: 'Unassigned',
    tags: [],
    customValues: [],
    checklistItems: [],
  };
}

function depthOf(task: DraftTask, tasks: DraftTask[]): number {
  let depth = 0;
  let current = task;
  const seen = new Set<string>();
  while (current.parentClientId && !seen.has(current.clientId)) {
    seen.add(current.clientId);
    const parent = tasks.find((t) => t.clientId === current.parentClientId);
    if (!parent) break;
    depth += 1;
    current = parent;
  }
  return depth;
}

// Native HTML5 drag events for reordering (no dnd-kit/react-beautiful-dnd — this app has no
// existing drag-and-drop library and the CLAUDE.md-adjacent convention across Kanban/Calendar is
// to use native draggable/onDragOver/onDrop directly rather than add one). Tasks are kept in one
// flat array whose array order IS the display/drag order; sortOrder sent to the backend is
// recomputed at save time as each task's position among siblings sharing the same parentClientId.
export function ProjectTemplateBuilderPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const isNew = id === 'new';
  const existing = useProjectTemplate(isNew ? undefined : id);
  const collaborators = useCollaborators();
  const createTemplate = useCreateProjectTemplate();
  const updateTemplate = useUpdateProjectTemplate(id ?? '');

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isPublic, setIsPublic] = useState(false);
  const [tasks, setTasks] = useState<DraftTask[]>([]);
  const [dependencies, setDependencies] = useState<SaveProjectTemplateDependencyRequest[]>([]);
  const [expandedKey, setExpandedKey] = useState<string | null>(null);
  const [dragKey, setDragKey] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // React's own recommended "adjust state during render" pattern for seeding local editable
  // state from a query result, rather than a useEffect(() => setState(...)) that would just
  // trigger an extra render after the one that already has the data (see ProfileSettingsPage's
  // own precedent for this exact pattern).
  const [loadedId, setLoadedId] = useState<string | undefined>(undefined);
  if (existing.data && existing.data.id !== loadedId) {
    const template = existing.data;
    setLoadedId(template.id);
    setName(template.name);
    setDescription(template.description ?? '');
    setIsPublic(template.isPublic);
    setTasks(
      [...template.tasks]
        .sort((a, b) => a.sortOrder - b.sortOrder)
        .map((t) => ({
          key: t.id,
          clientId: t.id,
          parentClientId: t.parentTemplateTaskId ?? undefined,
          title: t.title,
          description: t.description ?? undefined,
          status: t.status,
          priority: t.priority,
          startOffsetDays: t.startOffsetDays ?? undefined,
          dueOffsetDays: t.dueOffsetDays ?? undefined,
          estimatedDurationDays: t.estimatedDurationDays ?? undefined,
          assignmentMode: t.assignmentMode as TemplateAssignmentMode,
          assignedToUserId: t.assignedToUserId ?? undefined,
          tags: t.tags,
          customValues: t.customValues,
          checklistItems: t.checklistItems.map((c) => c.text),
        })),
    );
    setDependencies(
      template.dependencies.map((d) => ({
        templateTaskClientId: d.templateTaskId,
        dependsOnTemplateTaskClientId: d.dependsOnTemplateTaskId,
        dependencyType: d.dependencyType,
      })),
    );
  }

  function addTask() {
    const task = newDraftTask();
    setTasks([...tasks, task]);
    setExpandedKey(task.key);
  }

  function updateTask(key: string, patch: Partial<DraftTask>) {
    setTasks((prev) => prev.map((t) => (t.key === key ? { ...t, ...patch } : t)));
  }

  function removeTask(key: string) {
    const task = tasks.find((t) => t.key === key);
    if (!task) return;
    // Also drop any descendant subtasks and any dependency edge touching this task or its
    // descendants — leaving a dangling parentClientId/dependency reference would fail server-side
    // validation with a confusing "unknown task" error instead of this being handled in the UI.
    const toRemove = new Set<string>([task.clientId]);
    let changed = true;
    while (changed) {
      changed = false;
      for (const t of tasks) {
        if (t.parentClientId && toRemove.has(t.parentClientId) && !toRemove.has(t.clientId)) {
          toRemove.add(t.clientId);
          changed = true;
        }
      }
    }
    setTasks((prev) => prev.filter((t) => !toRemove.has(t.clientId)));
    setDependencies((prev) => prev.filter((d) => !toRemove.has(d.templateTaskClientId) && !toRemove.has(d.dependsOnTemplateTaskClientId)));
    if (expandedKey === key) setExpandedKey(null);
  }

  function handleDrop(targetKey: string) {
    if (!dragKey || dragKey === targetKey) {
      setDragKey(null);
      return;
    }
    setTasks((prev) => {
      const next = [...prev];
      const fromIndex = next.findIndex((t) => t.key === dragKey);
      const toIndex = next.findIndex((t) => t.key === targetKey);
      if (fromIndex === -1 || toIndex === -1) return prev;
      const [moved] = next.splice(fromIndex, 1);
      next.splice(toIndex, 0, moved);
      return next;
    });
    setDragKey(null);
  }

  function addDependency() {
    if (tasks.length < 2) return;
    setDependencies([
      ...dependencies,
      { templateTaskClientId: tasks[1].clientId, dependsOnTemplateTaskClientId: tasks[0].clientId, dependencyType: 'FinishToStart' },
    ]);
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmedName = name.trim();
    if (!trimmedName) return;
    if (tasks.some((t) => !t.title.trim())) {
      setError('Every task needs a title.');
      return;
    }
    setError(null);

    // sortOrder = position among siblings sharing the same parent, in current array order.
    const siblingCounters = new Map<string, number>();
    const requestTasks: SaveProjectTemplateTaskRequest[] = tasks.map((t) => {
      const siblingKey = t.parentClientId ?? '__root__';
      const sortOrder = siblingCounters.get(siblingKey) ?? 0;
      siblingCounters.set(siblingKey, sortOrder + 1);
      const { key: _key, ...rest } = t;
      return { ...rest, sortOrder };
    });

    const request = { name: trimmedName, description: description.trim() || undefined, isPublic, tasks: requestTasks, dependencies };
    const mutation = isNew ? createTemplate : updateTemplate;
    mutation.mutate(request, {
      onSuccess: () => navigate('/templates'),
      onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not save template.'),
    });
  }

  const isPending = createTemplate.isPending || updateTemplate.isPending;

  return (
    <div className="template-builder">
      <div className="template-builder__header">
        <button type="button" className="icon-button" onClick={() => navigate('/templates')} aria-label="Back to Templates">
          <ArrowLeft size={18} />
        </button>
        <h1>{isNew ? 'New Project Template' : 'Edit Project Template'}</h1>
      </div>

      <form className="template-builder__form" onSubmit={handleSubmit}>
        <label className="template-builder__field">
          Template Name
          <input type="text" value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
        </label>

        <label className="template-builder__field">
          Description
          <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={2} />
        </label>

        <label className="template-builder__field">
          <span className="template-builder__checkbox-label">
            <input type="checkbox" checked={isPublic} onChange={(e) => setIsPublic(e.target.checked)} />
            Public (visible to every user with template access, not just me)
          </span>
        </label>

        <div className="template-builder__section">
          <div className="template-builder__section-header">
            <h2>Tasks ({tasks.length})</h2>
            <button type="button" onClick={addTask}>
              <Plus size={14} /> Add Task
            </button>
          </div>

          {tasks.length === 0 && <p className="template-builder__empty">No tasks yet — add one to get started.</p>}

          <ul className="template-builder__task-list">
            {tasks.map((task) => (
              <li
                key={task.key}
                className="template-builder__task-row"
                style={{ marginLeft: depthOf(task, tasks) * 20 }}
                draggable
                onDragStart={() => setDragKey(task.key)}
                onDragOver={(e) => e.preventDefault()}
                onDrop={() => handleDrop(task.key)}
              >
                <div className="template-builder__task-summary">
                  <GripVertical size={14} className="template-builder__drag-handle" />
                  <button
                    type="button"
                    className="icon-button"
                    onClick={() => setExpandedKey(expandedKey === task.key ? null : task.key)}
                  >
                    {expandedKey === task.key ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                  </button>
                  <span className="template-builder__task-title">{task.title || '(untitled task)'}</span>
                  <span className="template-builder__task-badge">{task.priority}</span>
                  {task.parentClientId && <span className="template-builder__task-badge">subtask</span>}
                  <button type="button" className="template-builder__task-delete" onClick={() => removeTask(task.key)} aria-label="Delete task">
                    <Trash2 size={13} />
                  </button>
                </div>

                {expandedKey === task.key && (
                  <TaskEditor
                    task={task}
                    tasks={tasks}
                    collaborators={collaborators.map((c) => ({ id: c.id, name: c.name }))}
                    onChange={(patch) => updateTask(task.key, patch)}
                  />
                )}
              </li>
            ))}
          </ul>
        </div>

        <div className="template-builder__section">
          <div className="template-builder__section-header">
            <h2>Dependencies ({dependencies.length})</h2>
            <button type="button" onClick={addDependency} disabled={tasks.length < 2}>
              <Plus size={14} /> Add Dependency
            </button>
          </div>

          {dependencies.length === 0 && <p className="template-builder__empty">No dependencies between tasks yet.</p>}

          <ul className="template-builder__dependency-list">
            {dependencies.map((dep, index) => (
              <li key={index} className="template-builder__dependency-row">
                <select
                  value={dep.templateTaskClientId}
                  onChange={(e) =>
                    setDependencies((prev) => prev.map((d, i) => (i === index ? { ...d, templateTaskClientId: e.target.value } : d)))
                  }
                >
                  {tasks.map((t) => (
                    <option key={t.clientId} value={t.clientId}>
                      {t.title || '(untitled task)'}
                    </option>
                  ))}
                </select>
                <span>depends on</span>
                <select
                  value={dep.dependsOnTemplateTaskClientId}
                  onChange={(e) =>
                    setDependencies((prev) => prev.map((d, i) => (i === index ? { ...d, dependsOnTemplateTaskClientId: e.target.value } : d)))
                  }
                >
                  {tasks.map((t) => (
                    <option key={t.clientId} value={t.clientId}>
                      {t.title || '(untitled task)'}
                    </option>
                  ))}
                </select>
                <select
                  value={dep.dependencyType}
                  onChange={(e) =>
                    setDependencies((prev) => prev.map((d, i) => (i === index ? { ...d, dependencyType: e.target.value as DependencyType } : d)))
                  }
                >
                  {DEPENDENCY_TYPE_OPTIONS.map((type) => (
                    <option key={type} value={type}>
                      {DEPENDENCY_TYPE_LABELS[type]}
                    </option>
                  ))}
                </select>
                <button
                  type="button"
                  aria-label="Remove dependency"
                  onClick={() => setDependencies((prev) => prev.filter((_, i) => i !== index))}
                >
                  <X size={13} />
                </button>
              </li>
            ))}
          </ul>
        </div>

        {error && <p className="template-builder__error">{error}</p>}

        <div className="template-builder__form-actions">
          <button type="button" onClick={() => navigate('/templates')}>
            Cancel
          </button>
          <button type="submit" disabled={!name.trim() || isPending}>
            {isNew ? 'Create Template' : 'Save Changes'}
          </button>
        </div>
      </form>
    </div>
  );
}

interface TaskEditorProps {
  task: DraftTask;
  tasks: DraftTask[];
  collaborators: { id: string; name: string }[];
  onChange: (patch: Partial<DraftTask>) => void;
}

function TaskEditor({ task, tasks, collaborators, onChange }: TaskEditorProps) {
  const [tagDraft, setTagDraft] = useState('');
  const [checklistDraft, setChecklistDraft] = useState('');

  const parentCandidates = tasks.filter((t) => t.clientId !== task.clientId && depthOf(t, tasks) < 9);

  return (
    <div className="template-builder__task-editor">
      <label className="template-builder__field">
        Title
        <input type="text" value={task.title} onChange={(e) => onChange({ title: e.target.value })} required />
      </label>

      <label className="template-builder__field">
        Description
        <textarea value={task.description ?? ''} onChange={(e) => onChange({ description: e.target.value || undefined })} rows={2} />
      </label>

      <div className="template-builder__row">
        <label className="template-builder__field">
          Status
          <select value={task.status} onChange={(e) => onChange({ status: e.target.value as TaskStatus })}>
            {STATUS_OPTIONS.map((s) => (
              <option key={s} value={s}>
                {STATUS_LABELS[s]}
              </option>
            ))}
          </select>
        </label>
        <label className="template-builder__field">
          Priority
          <select value={task.priority} onChange={(e) => onChange({ priority: e.target.value as TaskPriority })}>
            {PRIORITY_OPTIONS.map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="template-builder__row">
        <label className="template-builder__field">
          Start Offset (days)
          <input
            type="number"
            value={task.startOffsetDays ?? ''}
            onChange={(e) => onChange({ startOffsetDays: e.target.value ? Number(e.target.value) : undefined })}
          />
        </label>
        <label className="template-builder__field">
          Due Offset (days)
          <input
            type="number"
            value={task.dueOffsetDays ?? ''}
            onChange={(e) => onChange({ dueOffsetDays: e.target.value ? Number(e.target.value) : undefined })}
          />
        </label>
        <label className="template-builder__field">
          Est. Duration (days)
          <input
            type="number"
            min={0}
            value={task.estimatedDurationDays ?? ''}
            onChange={(e) => onChange({ estimatedDurationDays: e.target.value ? Number(e.target.value) : undefined })}
          />
        </label>
      </div>

      <div className="template-builder__row">
        <label className="template-builder__field">
          Assignment
          <select value={task.assignmentMode} onChange={(e) => onChange({ assignmentMode: e.target.value as TemplateAssignmentMode })}>
            {TEMPLATE_ASSIGNMENT_MODE_OPTIONS.map((mode) => (
              <option key={mode} value={mode}>
                {TEMPLATE_ASSIGNMENT_MODE_LABELS[mode]}
              </option>
            ))}
          </select>
        </label>
        {task.assignmentMode === 'SpecificUser' && (
          <label className="template-builder__field">
            Specific User
            <select value={task.assignedToUserId ?? ''} onChange={(e) => onChange({ assignedToUserId: e.target.value || undefined })} required>
              <option value="" disabled>
                Select a user...
              </option>
              {collaborators.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                </option>
              ))}
            </select>
          </label>
        )}
        <label className="template-builder__field">
          Parent Task (subtask of)
          <select value={task.parentClientId ?? ''} onChange={(e) => onChange({ parentClientId: e.target.value || undefined })}>
            <option value="">None (top level)</option>
            {parentCandidates.map((t) => (
              <option key={t.clientId} value={t.clientId}>
                {t.title || '(untitled task)'}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="template-builder__field">
        Tags
        <div className="template-builder__chips">
          {task.tags.map((tag) => (
            <span className="tag-chip" key={tag}>
              {tag}
              <button type="button" aria-label={`Remove ${tag}`} onClick={() => onChange({ tags: task.tags.filter((t) => t !== tag) })}>
                <X size={10} />
              </button>
            </span>
          ))}
          <span className="template-builder__chip-add">
            <input
              type="text"
              placeholder="Add tag..."
              value={tagDraft}
              onChange={(e) => setTagDraft(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  const trimmed = tagDraft.trim();
                  if (trimmed && !task.tags.includes(trimmed)) onChange({ tags: [...task.tags, trimmed] });
                  setTagDraft('');
                }
              }}
            />
          </span>
        </div>
      </div>

      <div className="template-builder__field">
        Checklist
        <ul className="template-builder__checklist">
          {task.checklistItems.map((item, index) => (
            <li key={index}>
              <span>{item}</span>
              <button
                type="button"
                aria-label={`Remove ${item}`}
                onClick={() => onChange({ checklistItems: task.checklistItems.filter((_, i) => i !== index) })}
              >
                <X size={10} />
              </button>
            </li>
          ))}
        </ul>
        <span className="template-builder__chip-add">
          <input
            type="text"
            placeholder="Add checklist item..."
            value={checklistDraft}
            onChange={(e) => setChecklistDraft(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                const trimmed = checklistDraft.trim();
                if (trimmed) onChange({ checklistItems: [...task.checklistItems, trimmed] });
                setChecklistDraft('');
              }
            }}
          />
        </span>
      </div>
    </div>
  );
}
