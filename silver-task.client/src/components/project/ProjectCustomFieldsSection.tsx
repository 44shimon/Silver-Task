import { useMemo, useState, type ReactNode } from 'react';
import { ChevronDown } from 'lucide-react';
import type { CustomField } from '@/types/customField';
import type { Project, UserSummary } from '@/types/project';
import { useCustomFields, useSetProjectCustomValue } from '@/hooks/useCustomFields';
import { useProjects } from '@/hooks/useProjects';
import { useTasks } from '@/hooks/useTasks';
import { ApiError } from '@/api/httpClient';
// Reuses the existing dropdown/multiselect popover classes rather than redefining them.
import '@/components/spreadsheet/DropdownCell.css';
import '@/components/spreadsheet/MultiSelectCustomValueCell.css';
import './ProjectCustomFieldsSection.css';

const TEXT_INPUT_TYPE_BY_FIELD_TYPE: Partial<Record<string, string>> = {
  Number: 'number',
  Currency: 'number',
  Url: 'url',
  Email: 'email',
  Phone: 'tel',
};

interface ProjectCustomFieldsSectionProps {
  project: Project;
  members: UserSummary[];
  canEdit: boolean;
}

/** Phase 41 — the Project detail page's own "Custom Fields" section (spec #11's own mockup:
 * plain Label / value pairs, grouped, editable inline). Deliberately a self-contained field
 * editor rather than reusing the spreadsheet's *CustomValueCell components — those are hardwired
 * to useSetTaskCustomValue/grid-cell styling, and generalizing all six of them to be
 * entity-agnostic was judged a bigger risk than one focused component here (a disclosed,
 * pragmatic tradeoff — see the Phase 41 final report). */
export function ProjectCustomFieldsSection({ project, members, canEdit }: ProjectCustomFieldsSectionProps) {
  const { data: fields } = useCustomFields(project.id, 'Project');

  const grouped = useMemo(() => {
    const active = (fields ?? []).filter((f) => f.isActive || project.customValues.some((v) => v.customFieldId === f.id));
    const sorted = [...active].sort((a, b) => a.sortOrder - b.sortOrder);
    const groups = new Map<string, CustomField[]>();
    for (const field of sorted) {
      const key = field.groupName ?? '';
      const list = groups.get(key) ?? [];
      list.push(field);
      groups.set(key, list);
    }
    return groups;
  }, [fields, project.customValues]);

  if (!fields || fields.length === 0) {
    return null;
  }

  return (
    <div className="project-custom-fields">
      <h2>Custom Fields</h2>
      {Array.from(grouped.entries()).map(([groupName, groupFields]) => (
        <div className="project-custom-fields__group" key={groupName || '__ungrouped'}>
          {groupName && <h3 className="project-custom-fields__group-title">{groupName}</h3>}
          {groupFields.map((field) => (
            <ProjectFieldRow key={field.id} project={project} field={field} members={members} canEdit={canEdit} />
          ))}
        </div>
      ))}
    </div>
  );
}

function isConditionSatisfied(field: CustomField, project: Project): boolean {
  if (!field.conditionFieldId || !field.conditionOperator) {
    return true;
  }
  const controllingValue = project.customValues.find((v) => v.customFieldId === field.conditionFieldId)?.value ?? null;
  const expected = field.conditionValue;

  switch (field.conditionOperator) {
    case 'IsEmpty':
      return !controllingValue;
    case 'IsNotEmpty':
      return Boolean(controllingValue);
    case 'Equals':
      return controllingValue === expected;
    case 'NotEquals':
      return controllingValue !== expected;
    case 'Contains':
      return Boolean(controllingValue && expected && controllingValue.includes(expected));
    case 'NotContains':
      return !(controllingValue && expected && controllingValue.includes(expected));
    case 'GreaterThan':
      return Boolean(controllingValue && expected && Number(controllingValue) > Number(expected));
    case 'LessThan':
      return Boolean(controllingValue && expected && Number(controllingValue) < Number(expected));
    case 'GreaterThanOrEqual':
      return Boolean(controllingValue && expected && Number(controllingValue) >= Number(expected));
    case 'LessThanOrEqual':
      return Boolean(controllingValue && expected && Number(controllingValue) <= Number(expected));
    case 'Before':
      return Boolean(controllingValue && expected && controllingValue < expected);
    case 'After':
      return Boolean(controllingValue && expected && controllingValue > expected);
    default:
      return true;
  }
}

function ProjectFieldRow({
  project,
  field,
  members,
  canEdit,
}: {
  project: Project;
  field: CustomField;
  members: UserSummary[];
  canEdit: boolean;
}) {
  if (!isConditionSatisfied(field, project)) {
    return null;
  }

  const value = project.customValues.find((v) => v.customFieldId === field.id)?.value ?? null;

  return (
    <div className="project-custom-fields__row">
      <span className="project-custom-fields__label" title={field.description ?? undefined}>
        {field.name}
        {field.isRequired && <span className="project-custom-fields__required">*</span>}
      </span>
      <ProjectFieldValueEditor project={project} field={field} value={value} members={members} canEdit={canEdit} />
      {field.description && <span className="project-custom-fields__hint">{field.description}</span>}
    </div>
  );
}

function ProjectFieldValueEditor({
  project,
  field,
  value,
  members,
  canEdit,
}: {
  project: Project;
  field: CustomField;
  value: string | null;
  members: UserSummary[];
  canEdit: boolean;
}) {
  const setValue = useSetProjectCustomValue(project.id);
  const [draft, setDraft] = useState(value ?? '');
  const [isEditing, setIsEditing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { data: allProjects } = useProjects();
  const { data: projectTasks } = useTasks(field.fieldType === 'TaskReference' ? project.id : undefined);

  function commit(nextValue: string | null) {
    setError(null);
    setValue.mutate(
      { customFieldId: field.id, value: nextValue },
      { onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not save value.') },
    );
  }

  function commitDraft() {
    setIsEditing(false);
    const trimmed = draft.trim();
    if (trimmed !== (value ?? '')) {
      commit(trimmed || null);
    }
  }

  if (!canEdit) {
    return <span className="project-custom-fields__value">{formatReadOnly(field, value, members, allProjects, projectTasks)}</span>;
  }

  return (
    <span className="project-custom-fields__editor">
      {renderEditor()}
      {error && <span className="project-custom-fields__error">{error}</span>}
    </span>
  );

  function renderEditor(): ReactNode {
  switch (field.fieldType) {
    case 'Text':
    case 'LongText':
    case 'Number':
    case 'Currency':
    case 'Url':
    case 'Email':
    case 'Phone':
      return isEditing ? (
        field.fieldType === 'LongText' ? (
          <textarea
            className="project-custom-fields__input"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onBlur={commitDraft}
            autoFocus
          />
        ) : (
          <input
            className="project-custom-fields__input"
            type={TEXT_INPUT_TYPE_BY_FIELD_TYPE[field.fieldType] ?? 'text'}
            step={field.fieldType === 'Currency' ? '0.01' : undefined}
            maxLength={field.maxLength ?? undefined}
            placeholder={field.placeholder ?? undefined}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onBlur={commitDraft}
            onKeyDown={(e) => e.key === 'Enter' && e.currentTarget.blur()}
            autoFocus
          />
        )
      ) : (
        <span
          className="project-custom-fields__value project-custom-fields__value--editable"
          onClick={() => {
            setDraft(value ?? '');
            setIsEditing(true);
          }}
        >
          {formatReadOnly(field, value, members, allProjects, projectTasks)}
        </span>
      );

    case 'Date':
      return (
        <input
          className="project-custom-fields__input"
          type="date"
          value={value ?? ''}
          onChange={(e) => commit(e.target.value || null)}
        />
      );

    case 'DateTime':
      return (
        <input
          className="project-custom-fields__input"
          type="datetime-local"
          value={value ? value.slice(0, 16) : ''}
          onChange={(e) => commit(e.target.value ? new Date(e.target.value).toISOString() : null)}
        />
      );

    case 'Checkbox':
      return (
        <label className="project-custom-fields__checkbox">
          <input type="checkbox" checked={value === 'true'} onChange={(e) => commit(e.target.checked ? 'true' : 'false')} />
          {value === 'true' ? 'Yes' : 'No'}
        </label>
      );

    case 'Dropdown':
    case 'User':
    case 'TaskReference':
    case 'ProjectReference':
      return (
        <SelectEditor
          value={value}
          onChange={commit}
          options={
            field.fieldType === 'Dropdown'
              ? field.options.map((o) => ({ id: o.id, label: o.value }))
              : field.fieldType === 'User'
                ? members.map((m) => ({ id: m.id, label: m.name }))
                : field.fieldType === 'TaskReference'
                  ? (projectTasks ?? []).map((t) => ({ id: t.id, label: t.title }))
                  : (allProjects ?? []).map((p) => ({ id: p.id, label: p.name }))
          }
        />
      );

    case 'MultiSelect':
    case 'UserMulti':
      return (
        <MultiSelectEditor
          value={value}
          onChange={commit}
          options={
            field.fieldType === 'MultiSelect'
              ? field.options.map((o) => ({ id: o.id, label: o.value }))
              : members.map((m) => ({ id: m.id, label: m.name }))
          }
        />
      );

    case 'Link':
      return <LinkEditor value={value} onChange={commit} />;

    default:
      return null;
  }
  }
}

function formatReadOnly(
  field: CustomField,
  value: string | null,
  members: UserSummary[],
  allProjects: { id: string; name: string }[] | undefined,
  projectTasks: { id: string; title: string }[] | undefined,
): ReactNode {
  if (!value) {
    return <span className="project-custom-fields__placeholder">—</span>;
  }
  if (field.fieldType === 'Currency') return `$${value}`;
  if (field.fieldType === 'Checkbox') return value === 'true' ? 'Yes' : 'No';
  if (field.fieldType === 'User') return members.find((m) => m.id === value)?.name ?? value;
  if (field.fieldType === 'TaskReference') return projectTasks?.find((t) => t.id === value)?.title ?? value;
  if (field.fieldType === 'ProjectReference') return allProjects?.find((p) => p.id === value)?.name ?? value;
  if (field.fieldType === 'Dropdown') return field.options.find((o) => o.id === value)?.value ?? value;
  if (field.fieldType === 'Link') {
    try {
      const parsed = JSON.parse(value) as { label?: string; url: string };
      return (
        <a href={parsed.url} target="_blank" rel="noreferrer">
          {parsed.label || parsed.url}
        </a>
      );
    } catch {
      return value;
    }
  }
  if (field.fieldType === 'Url') {
    return (
      <a href={value} target="_blank" rel="noreferrer">
        {value}
      </a>
    );
  }
  return value;
}

function SelectEditor({
  value,
  onChange,
  options,
}: {
  value: string | null;
  onChange: (v: string | null) => void;
  options: { id: string; label: string }[];
}) {
  return (
    <div className="dropdown-cell-wrapper dropdown-cell-wrapper--plain">
      <select
        className="dropdown-cell dropdown-cell--plain"
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value || null)}
      >
        <option value="">None</option>
        {options.map((o) => (
          <option key={o.id} value={o.id}>
            {o.label}
          </option>
        ))}
      </select>
      <ChevronDown size={12} className="dropdown-cell__chevron" />
    </div>
  );
}

function MultiSelectEditor({
  value,
  onChange,
  options,
}: {
  value: string | null;
  onChange: (v: string | null) => void;
  options: { id: string; label: string }[];
}) {
  const selectedIds: string[] = (() => {
    try {
      return value ? (JSON.parse(value) as string[]) : [];
    } catch {
      return [];
    }
  })();
  const selectedLabels = options.filter((o) => selectedIds.includes(o.id)).map((o) => o.label);

  function toggle(id: string) {
    const next = selectedIds.includes(id) ? selectedIds.filter((i) => i !== id) : [...selectedIds, id];
    onChange(next.length > 0 ? JSON.stringify(next) : null);
  }

  return (
    <details className="multiselect-cell">
      <summary className="multiselect-cell__summary">
        <span className="multiselect-cell__label">
          {selectedLabels.length > 0 ? selectedLabels.join(', ') : <span className="project-custom-fields__placeholder">—</span>}
        </span>
        <ChevronDown size={12} className="dropdown-cell__chevron" />
      </summary>
      <div className="multiselect-cell__panel">
        {options.map((option) => (
          <label key={option.id} className="multiselect-cell__option">
            <input type="checkbox" checked={selectedIds.includes(option.id)} onChange={() => toggle(option.id)} />
            <span>{option.label}</span>
          </label>
        ))}
        {options.length === 0 && <p className="multiselect-cell__empty">No options.</p>}
      </div>
    </details>
  );
}

function LinkEditor({ value, onChange }: { value: string | null; onChange: (v: string | null) => void }) {
  const parsed = (() => {
    try {
      return value ? (JSON.parse(value) as { label?: string; url: string }) : { label: '', url: '' };
    } catch {
      return { label: '', url: '' };
    }
  })();
  const [label, setLabel] = useState(parsed.label ?? '');
  const [url, setUrl] = useState(parsed.url ?? '');

  function commit() {
    if (!url.trim()) {
      onChange(null);
      return;
    }
    onChange(JSON.stringify({ label: label.trim(), url: url.trim() }));
  }

  return (
    <span className="project-custom-fields__link-editor">
      <input placeholder="Label" value={label} onChange={(e) => setLabel(e.target.value)} onBlur={commit} />
      <input placeholder="https://..." value={url} onChange={(e) => setUrl(e.target.value)} onBlur={commit} />
    </span>
  );
}
