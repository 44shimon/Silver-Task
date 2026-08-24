import { useState, type KeyboardEvent } from 'react';
import type { AdminUser } from '@/types/admin';
import { buildUserUpdateRequest, useUpdateUser } from '@/hooks/useAdminUsers';
import '@/components/spreadsheet/EditableCell.css';

interface EditableUserNameCellProps {
  user: AdminUser;
}

// Same click-to-edit/Enter-commits/Escape-cancels interaction as EditableTitleCell.
export function EditableUserNameCell({ user }: EditableUserNameCellProps) {
  const updateUser = useUpdateUser();
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState(user.name);

  function startEditing() {
    setDraft(user.name);
    setIsEditing(true);
  }

  function commit() {
    setIsEditing(false);
    const trimmed = draft.trim();
    if (trimmed && trimmed !== user.name) {
      updateUser.mutate({ id: user.id, request: buildUserUpdateRequest(user, { name: trimmed }) });
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.currentTarget.blur();
    } else if (event.key === 'Escape') {
      setIsEditing(false);
    }
  }

  if (isEditing) {
    return (
      <input
        className="editable-cell__input"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={handleKeyDown}
        autoFocus
      />
    );
  }

  return (
    <div
      className={`editable-cell${updateUser.isError ? ' editable-cell--error' : ''}`}
      tabIndex={0}
      role="button"
      onClick={startEditing}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          startEditing();
        }
      }}
      title={updateUser.isError ? 'Could not save — click to try again' : undefined}
    >
      {user.name}
    </div>
  );
}
