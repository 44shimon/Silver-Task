import { Plus, X } from 'lucide-react';
import { useCustomFields } from '@/hooks/useCustomFields';
import { useProjects } from '@/hooks/useProjects';
import { useActiveTags } from '@/hooks/useTags';
import { useProjectMembers } from '@/hooks/useProjects';
import { PRIORITY_OPTIONS, STATUS_LABELS, STATUS_OPTIONS } from '@/types/task';
import type { CustomField } from '@/types/customField';
import { CONDITION_OPERATOR_LABELS, type CustomFieldConditionOperator } from '@/types/customField';
import {
  ASSIGNEE_ME,
  ASSIGNEE_UNASSIGNED,
  CUSTOM_FIELD_PREFIX,
  RELATIVE_DATE_LABELS,
  RELATIVE_DATE_OPTIONS,
  SAVED_VIEW_FIELDS,
  type SavedViewEntityType,
  type SavedViewFilterCondition,
  type SavedViewFilterGroup,
} from '@/types/savedView';
import './FilterBuilder.css';

const TASK_FIELD_OPTIONS = [
  { value: SAVED_VIEW_FIELDS.status, label: 'Status' },
  { value: SAVED_VIEW_FIELDS.priority, label: 'Priority' },
  { value: SAVED_VIEW_FIELDS.assigneeId, label: 'Assignee' },
  { value: SAVED_VIEW_FIELDS.projectId, label: 'Project' },
  { value: SAVED_VIEW_FIELDS.tagId, label: 'Tag' },
  { value: SAVED_VIEW_FIELDS.dueDate, label: 'Due Date' },
  { value: SAVED_VIEW_FIELDS.createdAt, label: 'Created' },
  { value: SAVED_VIEW_FIELDS.updatedAt, label: 'Updated' },
];

const PROJECT_FIELD_OPTIONS = [
  { value: SAVED_VIEW_FIELDS.createdAt, label: 'Created' },
  { value: SAVED_VIEW_FIELDS.updatedAt, label: 'Updated' },
];

const DATE_FIELDS = new Set<string>([SAVED_VIEW_FIELDS.dueDate, SAVED_VIEW_FIELDS.createdAt, SAVED_VIEW_FIELDS.updatedAt]);

interface FilterBuilderProps {
  group: SavedViewFilterGroup;
  onChange: (group: SavedViewFilterGroup) => void;
  entityType: SavedViewEntityType;
  /** Custom fields are per-project (or global) in this app — there's no single flat "every
   * custom field the caller can filter on" list, so the builder lets the user pick one project
   * to source the custom-field picker from. A disclosed scope simplification, see the Phase 43
   * final report. */
  referenceProjectId: string | null;
  onReferenceProjectChange: (projectId: string | null) => void;
  depth?: number;
}

/**
 * Phase 43 — the ONE reusable recursive AND/OR filter-builder component (spec's own explicit "no
 * separate filtering implementations" requirement), used both by the Saved View editor and (via
 * the same component) anywhere else a filter tree needs building. Mirrors the spec's own mockup:
 * "Match: [ALL/ANY] / Field [operator] Value / AND / Field [operator] Value / [+Add Filter]
 * [+Add Filter Group]".
 */
export function FilterBuilder({ group, onChange, entityType, referenceProjectId, onReferenceProjectChange, depth = 0 }: FilterBuilderProps) {
  const { data: projects } = useProjects();
  const fieldOptions = entityType === 'Project' ? PROJECT_FIELD_OPTIONS : TASK_FIELD_OPTIONS;
  const { data: customFields } = useCustomFields(referenceProjectId ?? undefined, entityType);

  function updateCondition(index: number, next: SavedViewFilterCondition) {
    const conditions = [...group.conditions];
    conditions[index] = next;
    onChange({ ...group, conditions });
  }

  function removeCondition(index: number) {
    onChange({ ...group, conditions: group.conditions.filter((_, i) => i !== index) });
  }

  function addCondition() {
    const field = fieldOptions[0]?.value ?? SAVED_VIEW_FIELDS.status;
    onChange({ ...group, conditions: [...group.conditions, { field, operator: 'Equals', value: null, valueTo: null }] });
  }

  function addGroup() {
    onChange({ ...group, groups: [...group.groups, { logic: 'AND', conditions: [], groups: [] }] });
  }

  function updateGroup(index: number, next: SavedViewFilterGroup) {
    const groups = [...group.groups];
    groups[index] = next;
    onChange({ ...group, groups });
  }

  function removeGroup(index: number) {
    onChange({ ...group, groups: group.groups.filter((_, i) => i !== index) });
  }

  const isEmpty = group.conditions.length === 0 && group.groups.length === 0;

  return (
    <div className={`filter-builder${depth > 0 ? ' filter-builder--nested' : ''}`}>
      <div className="filter-builder__header">
        <span>Match:</span>
        <select value={group.logic} onChange={(e) => onChange({ ...group, logic: e.target.value as 'AND' | 'OR' })} aria-label="Match logic">
          <option value="AND">ALL (AND)</option>
          <option value="OR">ANY (OR)</option>
        </select>
        {isEmpty && <span className="filter-builder__empty-hint">No filters — matches everything you can access</span>}
      </div>

      {depth === 0 && customFields !== undefined && (
        <label className="filter-builder__reference-project">
          Custom field source project
          <select value={referenceProjectId ?? ''} onChange={(e) => onReferenceProjectChange(e.target.value || null)}>
            <option value="">Choose a project to pick custom fields from...</option>
            {projects?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </label>
      )}

      <div className="filter-builder__rows">
        {group.conditions.map((condition, index) => (
          <div className="filter-builder__row" key={index}>
            {index > 0 && <span className="filter-builder__connector">{group.logic}</span>}
            <FilterConditionEditor
              condition={condition}
              onChange={(next) => updateCondition(index, next)}
              fieldOptions={fieldOptions}
              customFields={customFields ?? []}
            />
            <button type="button" className="icon-button" aria-label="Remove condition" onClick={() => removeCondition(index)}>
              <X size={14} />
            </button>
          </div>
        ))}

        {group.groups.map((subgroup, index) => (
          <div className="filter-builder__row filter-builder__row--group" key={`group-${index}`}>
            {(group.conditions.length > 0 || index > 0) && <span className="filter-builder__connector">{group.logic}</span>}
            <div className="filter-builder__subgroup">
              <FilterBuilder
                group={subgroup}
                onChange={(next) => updateGroup(index, next)}
                entityType={entityType}
                referenceProjectId={referenceProjectId}
                onReferenceProjectChange={onReferenceProjectChange}
                depth={depth + 1}
              />
            </div>
            <button type="button" className="icon-button" aria-label="Remove filter group" onClick={() => removeGroup(index)}>
              <X size={14} />
            </button>
          </div>
        ))}
      </div>

      <div className="filter-builder__actions">
        <button type="button" onClick={addCondition}>
          <Plus size={13} /> Add Filter
        </button>
        <button type="button" onClick={addGroup}>
          <Plus size={13} /> Add Filter Group
        </button>
      </div>
    </div>
  );
}

function FilterConditionEditor({
  condition,
  onChange,
  fieldOptions,
  customFields,
}: {
  condition: SavedViewFilterCondition;
  onChange: (condition: SavedViewFilterCondition) => void;
  fieldOptions: { value: string; label: string }[];
  customFields: CustomField[];
}) {
  const isCustomField = condition.field.startsWith(CUSTOM_FIELD_PREFIX);
  const customField = isCustomField ? customFields.find((f) => CUSTOM_FIELD_PREFIX + f.id === condition.field) : undefined;

  function setField(field: string) {
    onChange({ field, operator: 'Equals', value: null, valueTo: null });
  }

  return (
    <div className="filter-condition">
      <select
        value={condition.field}
        onChange={(e) => setField(e.target.value)}
        aria-label="Filter field"
      >
        <optgroup label="Fields">
          {fieldOptions.map((f) => (
            <option key={f.value} value={f.value}>
              {f.label}
            </option>
          ))}
        </optgroup>
        {customFields.length > 0 && (
          <optgroup label="Custom Fields">
            {customFields.map((f) => (
              <option key={f.id} value={CUSTOM_FIELD_PREFIX + f.id}>
                {f.name}
              </option>
            ))}
          </optgroup>
        )}
      </select>

      {isCustomField ? (
        <CustomFieldConditionValue condition={condition} onChange={onChange} field={customField} />
      ) : (
        <BuiltInConditionValue condition={condition} onChange={onChange} />
      )}
    </div>
  );
}

function OperatorSelect({
  operator,
  operators,
  onChange,
}: {
  operator: CustomFieldConditionOperator;
  operators: CustomFieldConditionOperator[];
  onChange: (op: CustomFieldConditionOperator) => void;
}) {
  return (
    <select value={operator} onChange={(e) => onChange(e.target.value as CustomFieldConditionOperator)} aria-label="Operator">
      {operators.map((op) => (
        <option key={op} value={op}>
          {CONDITION_OPERATOR_LABELS[op]}
        </option>
      ))}
    </select>
  );
}

function BuiltInConditionValue({ condition, onChange }: { condition: SavedViewFilterCondition; onChange: (c: SavedViewFilterCondition) => void }) {
  if (condition.field === SAVED_VIEW_FIELDS.status) {
    return (
      <MultiCheckboxValue
        condition={condition}
        onChange={onChange}
        options={STATUS_OPTIONS.map((s) => ({ value: s, label: STATUS_LABELS[s] }))}
      />
    );
  }
  if (condition.field === SAVED_VIEW_FIELDS.priority) {
    return <MultiCheckboxValue condition={condition} onChange={onChange} options={PRIORITY_OPTIONS.map((p) => ({ value: p, label: p }))} />;
  }
  if (condition.field === SAVED_VIEW_FIELDS.projectId) {
    return <ProjectValue condition={condition} onChange={onChange} />;
  }
  if (condition.field === SAVED_VIEW_FIELDS.tagId) {
    return <TagValue condition={condition} onChange={onChange} />;
  }
  if (condition.field === SAVED_VIEW_FIELDS.assigneeId) {
    return <AssigneeValue condition={condition} onChange={onChange} />;
  }
  if (DATE_FIELDS.has(condition.field)) {
    return <DateValue condition={condition} onChange={onChange} />;
  }
  return null;
}

function MultiCheckboxValue({
  condition,
  onChange,
  options,
}: {
  condition: SavedViewFilterCondition;
  onChange: (c: SavedViewFilterCondition) => void;
  options: { value: string; label: string }[];
}) {
  const selected = new Set((condition.value ?? '').split(',').filter(Boolean));

  function toggle(value: string) {
    const next = new Set(selected);
    if (next.has(value)) next.delete(value);
    else next.add(value);
    onChange({ ...condition, value: [...next].join(',') || null });
  }

  return (
    <div className="filter-condition__multi">
      <OperatorSelect operator={condition.operator} operators={['Equals', 'NotEquals']} onChange={(op) => onChange({ ...condition, operator: op })} />
      <div className="filter-condition__checkboxes">
        {options.map((opt) => (
          <label key={opt.value}>
            <input type="checkbox" checked={selected.has(opt.value)} onChange={() => toggle(opt.value)} />
            {opt.label}
          </label>
        ))}
      </div>
    </div>
  );
}

function ProjectValue({ condition, onChange }: { condition: SavedViewFilterCondition; onChange: (c: SavedViewFilterCondition) => void }) {
  const { data: projects } = useProjects();
  return (
    <MultiCheckboxValue
      condition={condition}
      onChange={onChange}
      options={(projects ?? []).map((p) => ({ value: p.id, label: p.name }))}
    />
  );
}

function TagValue({ condition, onChange }: { condition: SavedViewFilterCondition; onChange: (c: SavedViewFilterCondition) => void }) {
  const { data: tags } = useActiveTags();
  return (
    <MultiCheckboxValue
      condition={condition}
      onChange={onChange}
      options={(tags ?? []).map((t) => ({ value: t.id, label: t.name }))}
    />
  );
}

function AssigneeValue({ condition, onChange }: { condition: SavedViewFilterCondition; onChange: (c: SavedViewFilterCondition) => void }) {
  const selected = new Set((condition.value ?? '').split(',').filter(Boolean));

  function toggle(value: string) {
    const next = new Set(selected);
    if (next.has(value)) next.delete(value);
    else next.add(value);
    onChange({ ...condition, value: [...next].join(',') || null });
  }

  return (
    <div className="filter-condition__multi">
      <OperatorSelect operator={condition.operator} operators={['Equals', 'NotEquals']} onChange={(op) => onChange({ ...condition, operator: op })} />
      <div className="filter-condition__checkboxes">
        <label>
          <input type="checkbox" checked={selected.has(ASSIGNEE_ME)} onChange={() => toggle(ASSIGNEE_ME)} />
          Me
        </label>
        <label>
          <input type="checkbox" checked={selected.has(ASSIGNEE_UNASSIGNED)} onChange={() => toggle(ASSIGNEE_UNASSIGNED)} />
          Unassigned
        </label>
      </div>
    </div>
  );
}

function DateValue({ condition, onChange }: { condition: SavedViewFilterCondition; onChange: (c: SavedViewFilterCondition) => void }) {
  const isDueDate = condition.field === SAVED_VIEW_FIELDS.dueDate;
  const relativeTokens = new Set(RELATIVE_DATE_OPTIONS as string[]);
  const isRelative = isDueDate && condition.value !== null && relativeTokens.has(condition.value);
  const mode = isDueDate && (isRelative || condition.value === null) ? 'relative' : 'custom';

  const operators: CustomFieldConditionOperator[] = ['Before', 'After', 'Between', 'IsEmpty', 'IsNotEmpty'];

  return (
    <div className="filter-condition__date">
      {isDueDate && (
        <div className="filter-condition__date-mode">
          <label>
            <input type="radio" checked={mode === 'relative'} onChange={() => onChange({ ...condition, operator: 'Equals', value: 'today', valueTo: null })} />
            Relative
          </label>
          <label>
            <input type="radio" checked={mode === 'custom'} onChange={() => onChange({ ...condition, operator: 'Before', value: null, valueTo: null })} />
            Custom Range
          </label>
        </div>
      )}

      {isDueDate && mode === 'relative' ? (
        <select value={condition.value ?? 'today'} onChange={(e) => onChange({ ...condition, operator: 'Equals', value: e.target.value, valueTo: null })}>
          {RELATIVE_DATE_OPTIONS.map((token) => (
            <option key={token} value={token}>
              {RELATIVE_DATE_LABELS[token]}
            </option>
          ))}
        </select>
      ) : (
        <>
          <OperatorSelect operator={condition.operator} operators={operators} onChange={(op) => onChange({ ...condition, operator: op })} />
          {condition.operator !== 'IsEmpty' && condition.operator !== 'IsNotEmpty' && (
            <>
              <input type="date" value={condition.value ?? ''} onChange={(e) => onChange({ ...condition, value: e.target.value || null })} />
              {condition.operator === 'Between' && (
                <input type="date" value={condition.valueTo ?? ''} onChange={(e) => onChange({ ...condition, valueTo: e.target.value || null })} />
              )}
            </>
          )}
        </>
      )}
    </div>
  );
}

const TEXT_OPERATORS: CustomFieldConditionOperator[] = ['Equals', 'NotEquals', 'Contains', 'NotContains', 'StartsWith', 'EndsWith', 'IsEmpty', 'IsNotEmpty'];
const NUMBER_OPERATORS: CustomFieldConditionOperator[] = ['Equals', 'NotEquals', 'GreaterThan', 'GreaterThanOrEqual', 'LessThan', 'LessThanOrEqual', 'Between', 'IsEmpty', 'IsNotEmpty'];
const DATE_OPERATORS: CustomFieldConditionOperator[] = ['Before', 'After', 'Between', 'IsEmpty', 'IsNotEmpty'];
const BOOLEAN_OPERATORS: CustomFieldConditionOperator[] = ['Equals'];
const DROPDOWN_OPERATORS: CustomFieldConditionOperator[] = ['Equals', 'NotEquals', 'IsEmpty', 'IsNotEmpty'];
const MULTISELECT_OPERATORS: CustomFieldConditionOperator[] = ['Contains', 'NotContains', 'IsEmpty', 'IsNotEmpty'];

function CustomFieldConditionValue({
  condition,
  onChange,
  field,
}: {
  condition: SavedViewFilterCondition;
  onChange: (c: SavedViewFilterCondition) => void;
  field: CustomField | undefined;
}) {
  const { data: members } = useProjectMembers(field?.projectId ?? undefined);

  if (!field) {
    return <span className="filter-condition__unavailable">Filter unavailable: field not found</span>;
  }

  const showValueInput = condition.operator !== 'IsEmpty' && condition.operator !== 'IsNotEmpty';
  const showRangeInput = condition.operator === 'Between';

  switch (field.fieldType) {
    case 'Text':
    case 'LongText':
    case 'Url':
    case 'Email':
    case 'Phone':
      return (
        <div className="filter-condition__multi">
          <OperatorSelect operator={condition.operator} operators={TEXT_OPERATORS} onChange={(op) => onChange({ ...condition, operator: op })} />
          {showValueInput && <input type="text" value={condition.value ?? ''} onChange={(e) => onChange({ ...condition, value: e.target.value || null })} />}
        </div>
      );

    case 'Number':
    case 'Currency':
      return (
        <div className="filter-condition__multi">
          <OperatorSelect operator={condition.operator} operators={NUMBER_OPERATORS} onChange={(op) => onChange({ ...condition, operator: op })} />
          {showValueInput && <input type="number" value={condition.value ?? ''} onChange={(e) => onChange({ ...condition, value: e.target.value || null })} />}
          {showRangeInput && <input type="number" value={condition.valueTo ?? ''} onChange={(e) => onChange({ ...condition, valueTo: e.target.value || null })} />}
        </div>
      );

    case 'Date':
    case 'DateTime':
      return (
        <div className="filter-condition__multi">
          <OperatorSelect operator={condition.operator} operators={DATE_OPERATORS} onChange={(op) => onChange({ ...condition, operator: op })} />
          {showValueInput && <input type="date" value={condition.value ?? ''} onChange={(e) => onChange({ ...condition, value: e.target.value || null })} />}
          {showRangeInput && <input type="date" value={condition.valueTo ?? ''} onChange={(e) => onChange({ ...condition, valueTo: e.target.value || null })} />}
        </div>
      );

    case 'Checkbox':
      return (
        <div className="filter-condition__multi">
          <OperatorSelect operator={condition.operator} operators={BOOLEAN_OPERATORS} onChange={(op) => onChange({ ...condition, operator: op })} />
          <select value={condition.value ?? 'true'} onChange={(e) => onChange({ ...condition, value: e.target.value })}>
            <option value="true">Yes</option>
            <option value="false">No</option>
          </select>
        </div>
      );

    case 'Dropdown':
      return (
        <div className="filter-condition__multi">
          <OperatorSelect operator={condition.operator} operators={DROPDOWN_OPERATORS} onChange={(op) => onChange({ ...condition, operator: op })} />
          {showValueInput && (
            <select value={condition.value ?? ''} onChange={(e) => onChange({ ...condition, value: e.target.value || null })}>
              <option value="">Choose...</option>
              {field.options.map((opt) => (
                <option key={opt.id} value={opt.id}>
                  {opt.value}
                </option>
              ))}
            </select>
          )}
        </div>
      );

    case 'MultiSelect':
      return (
        <div className="filter-condition__multi">
          <OperatorSelect operator={condition.operator} operators={MULTISELECT_OPERATORS} onChange={(op) => onChange({ ...condition, operator: op })} />
          {showValueInput && (
            <select value={condition.value ?? ''} onChange={(e) => onChange({ ...condition, value: e.target.value || null })}>
              <option value="">Choose...</option>
              {field.options.map((opt) => (
                <option key={opt.id} value={opt.id}>
                  {opt.value}
                </option>
              ))}
            </select>
          )}
        </div>
      );

    case 'User':
      return (
        <div className="filter-condition__multi">
          <OperatorSelect operator={condition.operator} operators={DROPDOWN_OPERATORS} onChange={(op) => onChange({ ...condition, operator: op })} />
          {showValueInput && (
            <select value={condition.value ?? ''} onChange={(e) => onChange({ ...condition, value: e.target.value || null })}>
              <option value="">Choose...</option>
              <option value={ASSIGNEE_ME}>Me</option>
              {members?.map((m) => (
                <option key={m.user.id} value={m.user.id}>
                  {m.user.name}
                </option>
              ))}
            </select>
          )}
        </div>
      );

    default:
      return (
        <div className="filter-condition__multi">
          <OperatorSelect operator={condition.operator} operators={TEXT_OPERATORS} onChange={(op) => onChange({ ...condition, operator: op })} />
          {showValueInput && <input type="text" value={condition.value ?? ''} onChange={(e) => onChange({ ...condition, value: e.target.value || null })} />}
        </div>
      );
  }
}
