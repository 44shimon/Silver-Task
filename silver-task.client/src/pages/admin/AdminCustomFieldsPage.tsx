import { useMemo, useState } from 'react';
import { useAdminCustomFields } from '@/hooks/useAdminCustomFields';
import { useAllProjectsForAdmin } from '@/hooks/useProjects';
import { AdminCustomFieldsTable } from '@/components/admin/AdminCustomFieldsTable';
import { CustomFieldFormModal } from '@/components/admin/CustomFieldFormModal';
import { CUSTOM_FIELD_TYPE_LABELS, CUSTOM_FIELD_TYPE_OPTIONS, type CustomFieldEntityType, type CustomFieldType } from '@/types/customField';
import './AdminCustomFieldsPage.css';

type ActiveFilter = 'all' | 'active' | 'inactive';

export function AdminCustomFieldsPage() {
  const { data: projects } = useAllProjectsForAdmin();
  const [projectFilter, setProjectFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const [entityTypeFilter, setEntityTypeFilter] = useState('');
  const [activeFilter, setActiveFilter] = useState<ActiveFilter>('all');
  const [search, setSearch] = useState('');
  // An id, not the field object itself — the object goes stale the moment an option is
  // added/renamed/reordered/disabled inside the edit modal, since that mutation invalidates and
  // refetches `fields` rather than mutating whatever snapshot was captured on click.
  const [editingFieldId, setEditingFieldId] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  const { data: fields, isLoading, isError } = useAdminCustomFields({
    projectId: projectFilter || undefined,
    fieldType: (typeFilter as CustomFieldType) || undefined,
    entityType: (entityTypeFilter as CustomFieldEntityType) || undefined,
    isActive: activeFilter === 'all' ? undefined : activeFilter === 'active',
  });

  const filteredFields = useMemo(() => {
    const trimmed = search.trim().toLowerCase();
    if (!trimmed) return fields ?? [];
    return (fields ?? []).filter(
      (f) => f.name.toLowerCase().includes(trimmed) || f.identifier.toLowerCase().includes(trimmed),
    );
  }, [fields, search]);

  const editingField = fields?.find((f) => f.id === editingFieldId) ?? null;

  return (
    <div className="admin-custom-fields-page">
      <div className="admin-custom-fields-page__toolbar">
        <input
          type="search"
          placeholder="Search by name or identifier..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />

        <select value={projectFilter} onChange={(e) => setProjectFilter(e.target.value)}>
          <option value="">All Projects</option>
          {projects?.map((project) => (
            <option key={project.id} value={project.id}>
              {project.name}
            </option>
          ))}
        </select>

        <select value={entityTypeFilter} onChange={(e) => setEntityTypeFilter(e.target.value)}>
          <option value="">Task + Project Fields</option>
          <option value="Task">Task Fields</option>
          <option value="Project">Project Fields</option>
        </select>

        <select value={typeFilter} onChange={(e) => setTypeFilter(e.target.value)}>
          <option value="">All Types</option>
          {CUSTOM_FIELD_TYPE_OPTIONS.map((type) => (
            <option key={type} value={type}>
              {CUSTOM_FIELD_TYPE_LABELS[type]}
            </option>
          ))}
        </select>

        <select value={activeFilter} onChange={(e) => setActiveFilter(e.target.value as ActiveFilter)}>
          <option value="all">Active + Inactive</option>
          <option value="active">Active only</option>
          <option value="inactive">Inactive only</option>
        </select>

        <button type="button" className="admin-custom-fields-page__create" onClick={() => setShowCreate(true)}>
          + New Custom Field
        </button>
      </div>

      {isLoading && <p>Loading custom fields...</p>}
      {isError && <p>Custom fields could not be loaded.</p>}

      {!isLoading && !isError && (
        <AdminCustomFieldsTable fields={filteredFields} onEdit={(field) => setEditingFieldId(field.id)} />
      )}

      {showCreate && <CustomFieldFormModal mode="create" projects={projects ?? []} onClose={() => setShowCreate(false)} />}
      {editingField && (
        <CustomFieldFormModal mode="edit" field={editingField} projects={projects ?? []} onClose={() => setEditingFieldId(null)} />
      )}
    </div>
  );
}
