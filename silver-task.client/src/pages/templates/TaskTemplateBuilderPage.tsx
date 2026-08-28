import { useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, X } from 'lucide-react';
import { PRIORITY_OPTIONS, STATUS_LABELS, STATUS_OPTIONS, type TaskPriority, type TaskStatus } from '@/types/task';
import { TEMPLATE_ASSIGNMENT_MODE_LABELS, TEMPLATE_ASSIGNMENT_MODE_OPTIONS, type TemplateAssignmentMode } from '@/types/templates';
import { useCollaborators, useCreateTaskTemplate, useTaskTemplate, useUpdateTaskTemplate } from '@/hooks/useTemplates';
import { ApiError } from '@/api/httpClient';
import './TemplateBuilder.css';

/** The simpler of the two builders — a Task Template creates exactly one task (optionally with
 * subtasks-free checklist items) in an existing project the caller picks at use time. Field set
 * is deliberately capped at what CreateTaskRequest/TaskItem actually support (no
 * Category/TaskType — those concepts don't exist on the live task model, see the Phase 40 final
 * report's own disclosed note), per the spec's own "do not expose fields the model doesn't
 * support" instruction. */
export function TaskTemplateBuilderPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const isNew = id === 'new';
  const existing = useTaskTemplate(isNew ? undefined : id);
  const collaborators = useCollaborators();
  const createTemplate = useCreateTaskTemplate();
  const updateTemplate = useUpdateTaskTemplate(id ?? '');

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [status, setStatus] = useState<TaskStatus>('NotStarted');
  const [priority, setPriority] = useState<TaskPriority>('Medium');
  const [startOffsetDays, setStartOffsetDays] = useState('');
  const [dueOffsetDays, setDueOffsetDays] = useState('');
  const [estimatedDurationDays, setEstimatedDurationDays] = useState('');
  const [assignmentMode, setAssignmentMode] = useState<TemplateAssignmentMode>('Unassigned');
  const [assignedToUserId, setAssignedToUserId] = useState('');
  const [isPublic, setIsPublic] = useState(false);
  const [tags, setTags] = useState<string[]>([]);
  const [tagDraft, setTagDraft] = useState('');
  const [checklistItems, setChecklistItems] = useState<string[]>([]);
  const [checklistDraft, setChecklistDraft] = useState('');
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
    setStatus(template.status);
    setPriority(template.priority);
    setStartOffsetDays(template.startOffsetDays?.toString() ?? '');
    setDueOffsetDays(template.dueOffsetDays?.toString() ?? '');
    setEstimatedDurationDays(template.estimatedDurationDays?.toString() ?? '');
    setAssignmentMode(template.assignmentMode);
    setAssignedToUserId(template.assignedToUserId ?? '');
    setIsPublic(template.isPublic);
    setTags(template.tags);
    setChecklistItems(template.checklistItems.map((c) => c.text));
  }

  function addTag() {
    const trimmed = tagDraft.trim();
    if (trimmed && !tags.includes(trimmed)) {
      setTags([...tags, trimmed]);
    }
    setTagDraft('');
  }

  function addChecklistItem() {
    const trimmed = checklistDraft.trim();
    if (trimmed) {
      setChecklistItems([...checklistItems, trimmed]);
    }
    setChecklistDraft('');
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmedName = name.trim();
    if (!trimmedName) return;
    setError(null);

    const request = {
      name: trimmedName,
      description: description.trim() || undefined,
      status,
      priority,
      startOffsetDays: startOffsetDays ? Number(startOffsetDays) : undefined,
      dueOffsetDays: dueOffsetDays ? Number(dueOffsetDays) : undefined,
      estimatedDurationDays: estimatedDurationDays ? Number(estimatedDurationDays) : undefined,
      assignmentMode,
      assignedToUserId: assignmentMode === 'SpecificUser' ? assignedToUserId || undefined : undefined,
      isPublic,
      tags,
      customValues: [],
      checklistItems,
    };

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
        <h1>{isNew ? 'New Task Template' : 'Edit Task Template'}</h1>
      </div>

      <form className="template-builder__form" onSubmit={handleSubmit}>
        <label className="template-builder__field">
          Name
          <input type="text" value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
        </label>

        <label className="template-builder__field">
          Description
          <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3} />
        </label>

        <div className="template-builder__row">
          <label className="template-builder__field">
            Status
            <select value={status} onChange={(e) => setStatus(e.target.value as TaskStatus)}>
              {STATUS_OPTIONS.map((s) => (
                <option key={s} value={s}>
                  {STATUS_LABELS[s]}
                </option>
              ))}
            </select>
          </label>
          <label className="template-builder__field">
            Priority
            <select value={priority} onChange={(e) => setPriority(e.target.value as TaskPriority)}>
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
            Start Offset (days from anchor date)
            <input type="number" value={startOffsetDays} onChange={(e) => setStartOffsetDays(e.target.value)} />
          </label>
          <label className="template-builder__field">
            Due Offset (days from anchor date)
            <input type="number" value={dueOffsetDays} onChange={(e) => setDueOffsetDays(e.target.value)} />
          </label>
        </div>

        <label className="template-builder__field">
          Estimated Duration (days) — used to compute a due date only if no explicit due offset is set
          <input type="number" min={0} value={estimatedDurationDays} onChange={(e) => setEstimatedDurationDays(e.target.value)} />
        </label>

        <div className="template-builder__row">
          <label className="template-builder__field">
            Assignment
            <select value={assignmentMode} onChange={(e) => setAssignmentMode(e.target.value as TemplateAssignmentMode)}>
              {TEMPLATE_ASSIGNMENT_MODE_OPTIONS.map((mode) => (
                <option key={mode} value={mode}>
                  {TEMPLATE_ASSIGNMENT_MODE_LABELS[mode]}
                </option>
              ))}
            </select>
          </label>
          {assignmentMode === 'SpecificUser' && (
            <label className="template-builder__field">
              Specific User
              <select value={assignedToUserId} onChange={(e) => setAssignedToUserId(e.target.value)} required>
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
        </div>

        <label className="template-builder__field">
          <span className="template-builder__checkbox-label">
            <input type="checkbox" checked={isPublic} onChange={(e) => setIsPublic(e.target.checked)} />
            Public (visible to every user with template access, not just me)
          </span>
        </label>

        <div className="template-builder__field">
          Tags
          <div className="template-builder__chips">
            {tags.map((tag) => (
              <span className="tag-chip" key={tag}>
                {tag}
                <button type="button" aria-label={`Remove ${tag}`} onClick={() => setTags(tags.filter((t) => t !== tag))}>
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
                    addTag();
                  }
                }}
              />
              <button type="button" onClick={addTag} disabled={!tagDraft.trim()}>
                Add
              </button>
            </span>
          </div>
        </div>

        <div className="template-builder__field">
          Checklist
          <ul className="template-builder__checklist">
            {checklistItems.map((item, index) => (
              <li key={index}>
                <span>{item}</span>
                <button type="button" aria-label={`Remove ${item}`} onClick={() => setChecklistItems(checklistItems.filter((_, i) => i !== index))}>
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
                  addChecklistItem();
                }
              }}
            />
            <button type="button" onClick={addChecklistItem} disabled={!checklistDraft.trim()}>
              Add
            </button>
          </span>
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
