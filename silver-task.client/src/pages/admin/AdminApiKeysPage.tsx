import { useState } from 'react';
import { useApiKeys, useDeactivateServiceAccount, useRevokeApiKey, useServiceAccounts } from '@/hooks/useApiKeys';
import { CreateApiKeyDialog } from '@/components/admin/CreateApiKeyDialog';
import { RotateApiKeyDialog } from '@/components/admin/RotateApiKeyDialog';
import type { ApiKeySummary } from '@/types/apiKeys';
import './AdminApiKeysPage.css';

/** Admin -> API Keys (Phase 62) — administer service accounts and the API keys that authenticate
 * as them (or as a human user) against /api/v1/* via the X-Api-Key header. See
 * docs/api-keys.md / docs/n8n-integration.md. Administrator-only, matching every other Admin*
 * page's own RequireAdmin guard on the parent route. */
export function AdminApiKeysPage() {
  const { data: serviceAccounts, isLoading: accountsLoading } = useServiceAccounts();
  const { data: apiKeys, isLoading: keysLoading } = useApiKeys();
  const deactivateServiceAccount = useDeactivateServiceAccount();
  const revokeApiKey = useRevokeApiKey();

  const [creating, setCreating] = useState(false);
  const [rotating, setRotating] = useState<ApiKeySummary | null>(null);

  return (
    <div className="admin-api-keys-page">
      <div className="admin-api-keys-page__toolbar">
        <button type="button" className="admin-api-keys-page__new-button" onClick={() => setCreating(true)}>
          New API Key
        </button>
      </div>

      <section>
        <h2 className="admin-api-keys-page__section-title">API Keys</h2>
        {keysLoading && <p>Loading...</p>}
        {!keysLoading && apiKeys?.length === 0 && <p className="attachment-list__empty">No API keys yet.</p>}
        {!keysLoading && apiKeys && apiKeys.length > 0 && (
          <table className="admin-api-keys-page__table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Owner</th>
                <th>Prefix</th>
                <th>Status</th>
                <th>Last used</th>
                <th>Expires</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {apiKeys.map((key) => (
                <tr key={key.id}>
                  <td>{key.name}</td>
                  <td>{key.owner.name}</td>
                  <td>
                    <code>{key.keyPrefix}...</code>
                  </td>
                  <td>
                    <span className={`admin-api-keys-page__status admin-api-keys-page__status--${key.status.toLowerCase()}`}>
                      {key.status}
                    </span>
                  </td>
                  <td>{key.lastUsedAt ? new Date(key.lastUsedAt).toLocaleString() : 'Never'}</td>
                  <td>{key.expiresAt ? new Date(key.expiresAt).toLocaleDateString() : 'Never'}</td>
                  <td className="admin-api-keys-page__actions">
                    {key.status === 'Active' && (
                      <>
                        <button type="button" onClick={() => setRotating(key)}>
                          Rotate
                        </button>
                        <button type="button" onClick={() => revokeApiKey.mutate(key.id)}>
                          Revoke
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section>
        <h2 className="admin-api-keys-page__section-title">Service Accounts</h2>
        <p className="admin-api-keys-page__hint">
          A service account is a non-human identity (no password login — see docs/api-keys.md) that holds API keys.
          Add it to a project like any other member from that project's Members section, using its generated email.
        </p>
        {accountsLoading && <p>Loading...</p>}
        {!accountsLoading && serviceAccounts?.length === 0 && (
          <p className="attachment-list__empty">No service accounts yet.</p>
        )}
        {!accountsLoading && serviceAccounts && serviceAccounts.length > 0 && (
          <table className="admin-api-keys-page__table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Role</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {serviceAccounts.map((account) => (
                <tr key={account.id} className={!account.isActive ? 'admin-api-keys-page__row--inactive' : undefined}>
                  <td>{account.name}</td>
                  <td>{account.email}</td>
                  <td>{account.role}</td>
                  <td>{account.isActive ? 'Active' : 'Deactivated'}</td>
                  <td className="admin-api-keys-page__actions">
                    {account.isActive && (
                      <button type="button" onClick={() => deactivateServiceAccount.mutate(account.id)}>
                        Deactivate
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      {creating && <CreateApiKeyDialog onClose={() => setCreating(false)} />}
      {rotating && <RotateApiKeyDialog apiKey={rotating} onClose={() => setRotating(null)} />}
    </div>
  );
}
