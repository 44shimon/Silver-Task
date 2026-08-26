import { useState, type FormEvent } from 'react';
import {
  useAdminFileCategories,
  useCreateFileCategory,
  useDeleteFileCategory,
  useSetFileCategoryActive,
  useUpdateFileCategory,
} from '@/hooks/useFileCategories';
import { ApiError } from '@/api/httpClient';
import './AdminTagsPage.css';

/** Admin -> File Categories — create/rename/deactivate/reactivate/delete the shared global
 * category vocabulary. Deleting is refused (409) while any file still references the category;
 * deactivating is the recommended alternative (see FileCategoryService.DeleteAsync). */
export function AdminFileCategoriesPage() {
  const { data: categories, isLoading, isError } = useAdminFileCategories();
  const createCategory = useCreateFileCategory();
  const updateCategory = useUpdateFileCategory();
  const setActive = useSetFileCategoryActive();
  const deleteCategory = useDeleteFileCategory();
  const [newName, setNewName] = useState('');
  const [newDescription, setNewDescription] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draftName, setDraftName] = useState('');
  const [error, setError] = useState<string | null>(null);

  function handleCreate(event: FormEvent) {
    event.preventDefault();
    const trimmed = newName.trim();
    if (!trimmed) return;
    setError(null);
    createCategory.mutate(
      { name: trimmed, description: newDescription.trim() || undefined },
      {
        onSuccess: () => {
          setNewName('');
          setNewDescription('');
        },
        onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not create category.'),
      },
    );
  }

  function startEditing(id: string, currentName: string) {
    setEditingId(id);
    setDraftName(currentName);
    setError(null);
  }

  async function commitRename(id: string, description: string | null) {
    const trimmed = draftName.trim();
    setEditingId(null);
    if (!trimmed) return;
    try {
      await updateCategory.mutateAsync({ id, name: trimmed, description: description ?? undefined });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not rename category.');
    }
  }

  async function handleDelete(id: string) {
    setError(null);
    try {
      await deleteCategory.mutateAsync(id);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not delete category.');
    }
  }

  return (
    <div className="admin-tags-page">
      <h1>File Categories</h1>

      <form className="admin-tags-page__create-form" onSubmit={handleCreate}>
        <input type="text" placeholder="New category name" value={newName} onChange={(e) => setNewName(e.target.value)} />
        <input
          type="text"
          placeholder="Description (optional)"
          value={newDescription}
          onChange={(e) => setNewDescription(e.target.value)}
        />
        <button type="submit" disabled={createCategory.isPending || !newName.trim()}>
          Add
        </button>
      </form>

      {error && <p className="form-error">{error}</p>}

      {isLoading && <p>Loading categories...</p>}
      {isError && <p>Categories could not be loaded.</p>}

      {!isLoading && !isError && categories?.length === 0 && <p className="attachment-list__empty">No categories yet.</p>}

      {!isLoading && !isError && categories && categories.length > 0 && (
        <table className="admin-tags-page__table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Description</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {categories.map((category) => (
              <tr key={category.id} className={!category.isActive ? 'admin-tags-page__row--inactive' : undefined}>
                <td>
                  {editingId === category.id ? (
                    <input
                      value={draftName}
                      onChange={(e) => setDraftName(e.target.value)}
                      onBlur={() => commitRename(category.id, category.description)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') e.currentTarget.blur();
                        if (e.key === 'Escape') setEditingId(null);
                      }}
                      autoFocus
                    />
                  ) : (
                    <span onClick={() => startEditing(category.id, category.name)} title="Click to rename">
                      {category.name}
                    </span>
                  )}
                </td>
                <td>{category.description || '—'}</td>
                <td>{category.isActive ? 'Active' : 'Inactive'}</td>
                <td className="admin-tags-page__actions">
                  <button type="button" onClick={() => setActive.mutate({ id: category.id, isActive: !category.isActive })}>
                    {category.isActive ? 'Deactivate' : 'Activate'}
                  </button>
                  <button type="button" onClick={() => handleDelete(category.id)}>
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
