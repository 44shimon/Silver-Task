import { useState, type FormEvent } from 'react';
import { useAddProjectMember, useInviteProjectMember } from '@/hooks/useProjects';
import { ApiError } from '@/api/httpClient';
import './AddMemberSection.css';

interface AddMemberSectionProps {
  projectId: string;
  /** Only Administrators can create a new account via the invite fallback — a Manager/owner
   * hitting the same 404 still just sees the plain error and has to ask an Administrator. */
  isAdmin: boolean;
}

// "Add by email" is the normal path (existing accounts only). When that 404s because no
// account exists yet, an Administrator gets an inline fallback to create one and add them in
// the same step — there's no email/SMTP infrastructure in this app, so the temporary password
// is shared out-of-band by whoever creates it, same as Admin's password-reset flow.
export function AddMemberSection({ projectId, isAdmin }: AddMemberSectionProps) {
  const addMember = useAddProjectMember(projectId);
  const inviteMember = useInviteProjectMember(projectId);
  const [memberEmail, setMemberEmail] = useState('');
  const [notFoundEmail, setNotFoundEmail] = useState<string | null>(null);
  const [inviteName, setInviteName] = useState('');
  const [invitePassword, setInvitePassword] = useState('');

  function handleAddMember(event: FormEvent) {
    event.preventDefault();
    const trimmed = memberEmail.trim();
    if (!trimmed) {
      return;
    }

    setNotFoundEmail(null);
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

  function handleInvite(event: FormEvent) {
    event.preventDefault();
    if (!notFoundEmail) {
      return;
    }
    const trimmedName = inviteName.trim();
    if (!trimmedName || !invitePassword) {
      return;
    }

    inviteMember.mutate(
      { name: trimmedName, email: notFoundEmail, password: invitePassword },
      {
        onSuccess: () => {
          setMemberEmail('');
          setNotFoundEmail(null);
          setInviteName('');
          setInvitePassword('');
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
          onChange={(e) => {
            setMemberEmail(e.target.value);
            setNotFoundEmail(null);
          }}
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

      {notFoundEmail && !isAdmin && (
        <p className="form-error">No account found for {notFoundEmail}. Ask an Administrator to create one first.</p>
      )}

      {notFoundEmail && isAdmin && (
        <form className="invite-member-form" onSubmit={handleInvite}>
          <p className="invite-member-form__hint">
            No account found for <strong>{notFoundEmail}</strong>. Create one and add them to this project — share
            the password with them yourself, they can change it once they sign in.
          </p>
          <div className="invite-member-form__fields">
            <input
              type="text"
              placeholder="Full name"
              value={inviteName}
              onChange={(e) => setInviteName(e.target.value)}
              disabled={inviteMember.isPending}
            />
            <input
              type="password"
              placeholder="Temporary password (min 8 characters)"
              minLength={8}
              value={invitePassword}
              onChange={(e) => setInvitePassword(e.target.value)}
              disabled={inviteMember.isPending}
            />
            <button type="submit" disabled={inviteMember.isPending}>
              Create &amp; Add
            </button>
            <button type="button" className="invite-member-form__cancel" onClick={() => setNotFoundEmail(null)}>
              Cancel
            </button>
          </div>
          {inviteMember.isError && (
            <p className="form-error">
              {inviteMember.error instanceof ApiError ? inviteMember.error.message : 'Could not create account.'}
            </p>
          )}
        </form>
      )}
    </>
  );
}
