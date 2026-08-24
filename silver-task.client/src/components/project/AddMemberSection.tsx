import { useState, type FormEvent } from 'react';
import { useAddProjectMember } from '@/hooks/useProjects';
import { ApiError } from '@/api/httpClient';
import { UserNotFoundDialog } from './UserNotFoundDialog';

interface AddMemberSectionProps {
  projectId: string;
  /** Whether the currently logged-in user is an Administrator — only they get the "create an
   * account" link in the not-found dialog, since only Administrators can create users at all. */
  isAdmin: boolean;
}

// "Add by email" only works for existing accounts. When that 404s, a popup explains why —
// with a link to Admin → Users for Administrators to create the account there, then come back
// and add them normally. Two steps, but reuses the existing user-creation flow as-is instead of
// a second, parallel "create a user" code path.
export function AddMemberSection({ projectId, isAdmin }: AddMemberSectionProps) {
  const addMember = useAddProjectMember(projectId);
  const [memberEmail, setMemberEmail] = useState('');
  const [notFoundEmail, setNotFoundEmail] = useState<string | null>(null);

  function handleAddMember(event: FormEvent) {
    event.preventDefault();
    const trimmed = memberEmail.trim();
    if (!trimmed) {
      return;
    }

    addMember.mutate(
      { email: trimmed },
      {
        onSuccess: () => setMemberEmail(''),
        onError: (error) => {
          if (error instanceof ApiError && error.status === 404) {
            setNotFoundEmail(trimmed);
          }
        },
      },
    );
  }

  return (
    <>
      <form className="add-member-form" onSubmit={handleAddMember}>
        <input
          type="email"
          placeholder="Add member by email"
          value={memberEmail}
          onChange={(e) => setMemberEmail(e.target.value)}
          disabled={addMember.isPending}
        />
        <button type="submit" disabled={addMember.isPending}>
          Add
        </button>
      </form>

      {addMember.isError && !notFoundEmail && (
        <p className="form-error">
          {addMember.error instanceof ApiError ? addMember.error.message : 'Could not add member.'}
        </p>
      )}

      {notFoundEmail && (
        <UserNotFoundDialog email={notFoundEmail} isAdmin={isAdmin} onClose={() => setNotFoundEmail(null)} />
      )}
    </>
  );
}
