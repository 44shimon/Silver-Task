import { useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { AlertTriangle, ArrowLeft, ArrowRight, Check } from 'lucide-react';
import { useInstantiateProjectFromTemplate, useProjectTemplatePreview, useTemplatesList } from '@/hooks/useTemplates';
import { ApiError } from '@/api/httpClient';
import { formatDate } from '@/utils/formatDate';
import './CreateProjectFromTemplateWizardPage.css';

type Step = 'choose' | 'info' | 'dates' | 'assignments' | 'preview';

const STEPS: { key: Step; label: string }[] = [
  { key: 'choose', label: 'Choose Template' },
  { key: 'info', label: 'Project Information' },
  { key: 'dates', label: 'Configure Dates' },
  { key: 'assignments', label: 'Configure Assignments' },
  { key: 'preview', label: 'Preview' },
];

function todayIso(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

// The spec's own wizard flow: Choose Template -> Project Information -> Configure Dates ->
// Configure Assignments -> Preview -> Create. Project creation reuses ProjectService.CreateAsync
// through TemplateInstantiationService (see that service's own doc comment) — this page never
// talks to a separate/duplicated project-creation code path, and the caller is always the
// project's owner (no separate "Project Manager" picker; see CreateProjectFromTemplateRequest's
// own doc comment for why).
export function CreateProjectFromTemplateWizardPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const templates = useTemplatesList();
  const instantiate = useInstantiateProjectFromTemplate();

  const [step, setStep] = useState<Step>('choose');
  const [templateId, setTemplateId] = useState(searchParams.get('templateId') ?? '');
  const [search, setSearch] = useState('');
  const [projectName, setProjectName] = useState('');
  const [projectDescription, setProjectDescription] = useState('');
  const [startDate, setStartDate] = useState(todayIso());
  const [assignmentOverride, setAssignmentOverride] = useState<'' | 'Unassigned' | 'ProjectManager'>('');
  const [error, setError] = useState<string | null>(null);

  const projectTemplates = useMemo(
    () => (templates.data ?? []).filter((t) => t.type === 'Project' && !t.isArchived),
    [templates.data],
  );
  const filteredTemplates = useMemo(() => {
    const trimmed = search.trim().toLowerCase();
    if (!trimmed) return projectTemplates;
    return projectTemplates.filter((t) => t.name.toLowerCase().includes(trimmed) || (t.description?.toLowerCase().includes(trimmed) ?? false));
  }, [projectTemplates, search]);

  const selectedTemplate = projectTemplates.find((t) => t.id === templateId);
  const preview = useProjectTemplatePreview(step === 'preview' ? templateId : undefined, step === 'preview' ? startDate : undefined);

  const stepIndex = STEPS.findIndex((s) => s.key === step);

  function goNext() {
    if (step === 'choose' && templateId) setStep('info');
    else if (step === 'info' && projectName.trim()) setStep('dates');
    else if (step === 'dates' && startDate) setStep('assignments');
    else if (step === 'assignments') setStep('preview');
  }

  function goBack() {
    const prevIndex = stepIndex - 1;
    if (prevIndex >= 0) setStep(STEPS[prevIndex].key);
  }

  function handleCreate() {
    setError(null);
    instantiate.mutate(
      {
        templateId,
        projectName: projectName.trim(),
        projectDescription: projectDescription.trim() || undefined,
        startDate,
        assignmentOverride: assignmentOverride || undefined,
      },
      {
        onSuccess: (project) => navigate(`/projects/${project.id}`),
        onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not create project from template.'),
      },
    );
  }

  return (
    <div className="template-wizard">
      <div className="template-wizard__header">
        <button type="button" className="icon-button" onClick={() => navigate('/templates')} aria-label="Back to Templates">
          <ArrowLeft size={18} />
        </button>
        <h1>Create Project from Template</h1>
      </div>

      <ol className="template-wizard__steps">
        {STEPS.map((s, index) => (
          <li key={s.key} className={`template-wizard__step${index === stepIndex ? ' template-wizard__step--active' : ''}${index < stepIndex ? ' template-wizard__step--done' : ''}`}>
            {index < stepIndex ? <Check size={12} /> : index + 1}
            <span>{s.label}</span>
          </li>
        ))}
      </ol>

      <div className="template-wizard__body">
        {step === 'choose' && (
          <div className="template-wizard__panel">
            <input
              type="search"
              placeholder="Search project templates..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="template-wizard__search"
            />
            {templates.isLoading && <p>Loading templates...</p>}
            {!templates.isLoading && filteredTemplates.length === 0 && <p>No project templates available.</p>}
            <ul className="template-wizard__template-list">
              {filteredTemplates.map((t) => (
                <li
                  key={t.id}
                  className={`template-wizard__template-item${templateId === t.id ? ' template-wizard__template-item--selected' : ''}`}
                  onClick={() => setTemplateId(t.id)}
                >
                  <span className="template-wizard__template-name">{t.name}</span>
                  <span className="template-wizard__template-meta">
                    {t.taskCount} task{t.taskCount === 1 ? '' : 's'} · Used {t.usageCount} time{t.usageCount === 1 ? '' : 's'}
                  </span>
                  {t.description && <p>{t.description}</p>}
                </li>
              ))}
            </ul>
          </div>
        )}

        {step === 'info' && (
          <div className="template-wizard__panel">
            <label className="template-wizard__field">
              Project Name
              <input type="text" value={projectName} onChange={(e) => setProjectName(e.target.value)} required autoFocus />
            </label>
            <label className="template-wizard__field">
              Description (optional)
              <textarea value={projectDescription} onChange={(e) => setProjectDescription(e.target.value)} rows={3} />
            </label>
            <p className="template-wizard__hint">
              You'll be the project's owner — this app always creates new projects under the account that creates them.
            </p>
          </div>
        )}

        {step === 'dates' && (
          <div className="template-wizard__panel">
            <label className="template-wizard__field">
              Project Start Date
              <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} required />
            </label>
            <p className="template-wizard__hint">
              Every task's Start/Due date in "{selectedTemplate?.name}" is stored as an offset from this date, so shifting it
              shifts the whole schedule.
            </p>
          </div>
        )}

        {step === 'assignments' && (
          <div className="template-wizard__panel">
            <label className="template-wizard__field">
              Assignment Override
              <select value={assignmentOverride} onChange={(e) => setAssignmentOverride(e.target.value as typeof assignmentOverride)}>
                <option value="">Keep Template Assignment (use each task's own default)</option>
                <option value="ProjectManager">Assign All Tasks to Me (the project owner)</option>
                <option value="Unassigned">Leave All Tasks Unassigned</option>
              </select>
            </label>
            <p className="template-wizard__hint">
              Applies uniformly to every task. Per-task "Specific User" assignments in the template are always kept.
            </p>
          </div>
        )}

        {step === 'preview' && (
          <div className="template-wizard__panel">
            {preview.isLoading && <p>Computing schedule...</p>}
            {preview.data && (
              <>
                <div className="template-wizard__preview-summary">
                  <span>{preview.data.taskCount} tasks</span>
                  <span>{preview.data.subtaskCount} subtasks</span>
                  <span>{preview.data.dependencyCount} dependencies</span>
                  {preview.data.estimatedDurationDays !== null && <span>{preview.data.estimatedDurationDays} day span</span>}
                </div>

                {preview.data.warnings.length > 0 && (
                  <div className="template-wizard__warnings">
                    <AlertTriangle size={14} />
                    <ul>
                      {preview.data.warnings.map((w, i) => (
                        <li key={i}>{w}</li>
                      ))}
                    </ul>
                  </div>
                )}

                <ul className="template-wizard__schedule">
                  {preview.data.schedule.map((item) => (
                    <li key={item.templateTaskId}>
                      <span>{item.title}</span>
                      <span>
                        {item.computedStartDate ? formatDate(item.computedStartDate) : '—'}
                        {' → '}
                        {item.computedDueDate ? formatDate(item.computedDueDate) : '—'}
                      </span>
                    </li>
                  ))}
                </ul>
              </>
            )}
            {error && <p className="template-wizard__error">{error}</p>}
          </div>
        )}
      </div>

      <div className="template-wizard__actions">
        <button type="button" onClick={goBack} disabled={stepIndex === 0}>
          Back
        </button>
        {step !== 'preview' ? (
          <button
            type="button"
            className="template-wizard__primary"
            onClick={goNext}
            disabled={(step === 'choose' && !templateId) || (step === 'info' && !projectName.trim()) || (step === 'dates' && !startDate)}
          >
            Next <ArrowRight size={14} />
          </button>
        ) : (
          <button type="button" className="template-wizard__primary" onClick={handleCreate} disabled={instantiate.isPending}>
            Create Project
          </button>
        )}
      </div>
    </div>
  );
}
