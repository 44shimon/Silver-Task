import { useState, type FormEvent } from 'react';
import { Plus } from 'lucide-react';
import type { UserRole } from '@/types/auth';
import { useCreateUser } from '@/hooks/useAdminUsers';
import { ApiError } from '@/api/httpClient';
import './NewUserForm.css';

const ROLE_OPTIONS: UserRole[] = ['Member', 'Manager', 'Administrator', 'Viewer'];

export function NewUserForm() {
  const createUser = useCreateUser();
  const [isCreating, setIsCreating] = useState(false);
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<UserRole>('Member');

  function cancel() {
    setIsCreating(false);
    setName('');
    setEmail('');
    setPassword('');
    setRole('Member');
    createUser.reset();
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmedName = name.trim();
    const trimmedEmail = email.trim();
    if (!trimmedName || !trimmedEmail || !password) {
      return;
    }

    createUser.mutate(
      { name: trimmedName, email: trimmedEmail, password, role },
      { onSuccess: cancel },
    );
  }

  if (!isCreating) {
    return (
      <button type="button" className="new-user-button" onClick={() => setIsCreating(true)}>
        <Plus size={16} />
        <span>New User</span>
      </button>
    );
  }

  return (
    <form className="new-user-form" onSubmit={handleSubmit}>
      <input
        type="text"
        placeholder="Full name"
        value={name}
        onChange={(e) => setName(e.target.value)}
        autoFocus
        disabled={createUser.isPending}
      />
      <input
        type="email"
        placeholder="Email"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        disabled={createUser.isPending}
      />
      <input
        type="password"
        placeholder="Password (min 8 chars)"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        minLength={8}
        disabled={createUser.isPending}
      />
      <select value={role} onChange={(e) => setRole(e.target.value as UserRole)} disabled={createUser.isPending}>
        {ROLE_OPTIONS.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
      <button type="submit" disabled={createUser.isPending}>
        Create
      </button>
      <button type="button" className="new-user-form__cancel" onClick={cancel}>
        Cancel
      </button>
      {createUser.isError && (
        <span className="new-user-form__error">
          {createUser.error instanceof ApiError ? createUser.error.message : 'Could not create user.'}
        </span>
      )}
    </form>
  );
}
