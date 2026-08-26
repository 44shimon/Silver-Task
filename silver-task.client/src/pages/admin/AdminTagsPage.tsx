import { useState } from 'react';
import { useAdminTags, useDeleteTag, useRenameTag, useSetTagActive } from '@/hooks/useTags';
import { ApiError } from '@/api/httpClient';
import './AdminTagsPage.css';

/** Admin -> Tags — rename/deactivate/reactivate/delete the shared global tag vocabulary. Ad-hoc
 * tag *creation* happens inline while tagging a file (get-or-create), not here. */
export function AdminTagsPage() {
  const { data: tags, isLoading, isError } = useAdminTags();
  const renameTag = useRenameTag();
  const setActive = useSetTagActive();
  const deleteTag = useDeleteTag();
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draft, setDraft] = useState('');
  const [error, setError] = useState<string | null>(null);

  function startEditing(id: string, currentName: string) {
    setEditingId(id);
    setDraft(currentName);
    setError(null);
  }

  async function commitRename(id: string) {
    const trimmed = draft.trim();
    setEditingId(null);
    if (!trimmed) return;
    try {
      await renameTag.mutateAsync({ id, name: trimmed });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not rename tag.');
    }
  }

  async function handleDelete(id: string) {
    setError(null);
    try {
      await deleteTag.mutateAsync(id);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not delete tag.');
    }
  }

  return (
    <div className="admin-tags-page">
      <h1>Tags</h1>
      {error && <p className="form-error">{error}</p>}

      {isLoading && <p>Loading tags...</p>}
      {isError && <p>Tags could not be loaded.</p>}

      {!isLoading && !isError && tags?.length === 0 && <p className="attachment-list__empty">No tags yet.</p>}

      {!isLoading && !isError && tags && tags.length > 0 && (
        <table className="admin-tags-page__table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {tags.map((tag) => (
              <tr key={tag.id} className={!tag.isActive ? 'admin-tags-page__row--inactive' : undefined}>
                <td>
                  {editingId === tag.id ? (
                    <input
                      value={draft}
                      onChange={(e) => setDraft(e.target.value)}
                      onBlur={() => commitRename(tag.id)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') e.currentTarget.blur();
                        if (e.key === 'Escape') setEditingId(null);
                      }}
                      autoFocus
                    />
                  ) : (
                    <span onClick={() => startEditing(tag.id, tag.name)} title="Click to rename">
                      {tag.name}
                    </span>
                  )}
                </td>
                <td>{tag.isActive ? 'Active' : 'Inactive'}</td>
                <td className="admin-tags-page__actions">
                  <button type="button" onClick={() => setActive.mutate({ id: tag.id, isActive: !tag.isActive })}>
                    {tag.isActive ? 'Deactivate' : 'Activate'}
                  </button>
                  <button type="button" onClick={() => handleDelete(tag.id)}>
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
