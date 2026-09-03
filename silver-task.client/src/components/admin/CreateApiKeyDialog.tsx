import { useState, type FormEvent } from 'react';
import { Modal } from '@/components/shared/Modal';
import { ApiError } from '@/api/httpClient';
import { useCreateApiKey, useCreateServiceAccount, useServiceAccounts } from '@/hooks/useApiKeys';
import type { ApiKeyCreated } from '@/types/apiKeys';
import type { UserRole } from '@/types/auth';
import './CreateApiKeyDialog.css';

interface CreateApiKeyDialogProps {
  onClose: () => void;
}

const EXPIRY_OPTIONS = [
  { label: 'Never expires', days: null },
  { label: '30 days', days: 30 },
  { label: '90 days', days: 90 },
  { label: '1 year', days: 365 },
] as const;

const NEW_SERVICE_ACCOUNT_VALUE = '__new__';

/** Admin -> API Keys "New key" flow (Phase 62): pick an existing service account/user, or create
 * a new service account inline, name the key, pick an optional expiration, then show the raw key
 * exactly once — the same "shown once" invariant the backend enforces (ApiKey.KeyHash is the only
 * thing ever persisted). Reused for both create and rotate's "here's your new key" step by the
 * parent page passing an already-created ApiKeyCreated in via the `result` prop path — see
 * AdminApiKeysPage. */
export function CreateApiKeyDialog({ onClose }: CreateApiKeyDialogProps) {
  const { data: serviceAccounts } = useServiceAccounts();
  const createServiceAccount = useCreateServiceAccount();
  const createApiKey = useCreateApiKey();

  const [targetUserId, setTargetUserId] = useState(NEW_SERVICE_ACCOUNT_VALUE);
  const [newAccountName, setNewAccountName] = useState('');
  const [newAccountRole, setNewAccountRole] = useState<UserRole>('Member');
  const [keyName, setKeyName] = useState('');
  const [expiryDays, setExpiryDays] = useState<number | null>(90);
  const [error, setError] = useState<string | null>(null);
  const [created, setCreated] = useState<ApiKeyCreated | null>(null);
  const [copied, setCopied] = useState(false);

  const isPending = createServiceAccount.isPending || createApiKey.isPending;
  const activeAccounts = (serviceAccounts ?? []).filter((a) => a.isActive);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);

    try {
      let userId = targetUserId;
      if (userId === NEW_SERVICE_ACCOUNT_VALUE) {
        const account = await createServiceAccount.mutateAsync({ name: newAccountName, role: newAccountRole });
        userId = account.id;
      }

      const expiresAt = expiryDays === null ? null : new Date(Date.now() + expiryDays * 24 * 60 * 60 * 1000).toISOString();
      const result = await createApiKey.mutateAsync({ userId, name: keyName, expiresAt });
      setCreated(result);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not create the API key.');
    }
  }

  async function handleCopy() {
    if (!created) return;
    await navigator.clipboard.writeText(created.key);
    setCopied(true);
  }

  if (created) {
    return (
      <Modal onClose={onClose}>
        <h2>API key created</h2>
        <p className="create-api-key-dialog__warning">
          Copy this key now — it will never be shown again. If you lose it, revoke it and create a new one.
        </p>
        <div className="create-api-key-dialog__key-box">
          <code>{created.key}</code>
        </div>
        <div className="create-api-key-dialog__actions">
          <button type="button" className="create-api-key-dialog__copy" onClick={handleCopy}>
            {copied ? 'Copied' : 'Copy to clipboard'}
          </button>
          <button type="button" className="create-api-key-dialog__submit" onClick={onClose}>
            Done
          </button>
        </div>
      </Modal>
    );
  }

  return (
    <Modal onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <h2>New API key</h2>

        <label className="create-api-key-dialog__field">
          <span>Belongs to</span>
          <select value={targetUserId} onChange={(e) => setTargetUserId(e.target.value)}>
            <option value={NEW_SERVICE_ACCOUNT_VALUE}>+ Create a new service account</option>
            {activeAccounts.map((account) => (
              <option key={account.id} value={account.id}>
                {account.name} ({account.email})
              </option>
            ))}
          </select>
        </label>

        {targetUserId === NEW_SERVICE_ACCOUNT_VALUE && (
          <>
            <label className="create-api-key-dialog__field">
              <span>Service account name</span>
              <input
                value={newAccountName}
                onChange={(e) => setNewAccountName(e.target.value)}
                placeholder="e.g. n8n Production"
                required
                autoFocus
              />
            </label>
            <label className="create-api-key-dialog__field">
              <span>Role</span>
              <select value={newAccountRole} onChange={(e) => setNewAccountRole(e.target.value as UserRole)}>
                <option value="Member">Member</option>
                <option value="Manager">Manager</option>
                <option value="Viewer">Viewer</option>
                <option value="Administrator">Administrator</option>
              </select>
            </label>
          </>
        )}

        <label className="create-api-key-dialog__field">
          <span>Key name</span>
          <input
            value={keyName}
            onChange={(e) => setKeyName(e.target.value)}
            placeholder="e.g. n8n workflow — task sync"
            required
          />
        </label>

        <label className="create-api-key-dialog__field">
          <span>Expiration</span>
          <select
            value={expiryDays === null ? 'never' : String(expiryDays)}
            onChange={(e) => setExpiryDays(e.target.value === 'never' ? null : Number(e.target.value))}
          >
            {EXPIRY_OPTIONS.map((option) => (
              <option key={option.label} value={option.days === null ? 'never' : String(option.days)}>
                {option.label}
              </option>
            ))}
          </select>
        </label>

        {error && <p className="form-error">{error}</p>}

        <div className="create-api-key-dialog__actions">
          <button type="button" className="create-api-key-dialog__cancel" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="create-api-key-dialog__submit" disabled={isPending}>
            {isPending ? 'Creating...' : 'Create Key'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
