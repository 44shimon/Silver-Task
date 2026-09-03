import type { UserRole } from './auth';

/** Mirrors the backend's UserSummaryDto — the minimal shape nested inside a key's owner/creator. */
export interface ApiKeyUserSummary {
  id: string;
  name: string;
  email: string;
  isActive: boolean;
}

export type ApiKeyStatus = 'Active' | 'Revoked' | 'Expired';

/** Never carries the raw key or its hash — only keyPrefix (see the backend ApiKey entity's own
 * doc comment on why that's always safe to display). */
export interface ApiKeySummary {
  id: string;
  name: string;
  keyPrefix: string;
  status: ApiKeyStatus;
  owner: ApiKeyUserSummary;
  expiresAt: string | null;
  revokedAt: string | null;
  lastUsedAt: string | null;
  createdAt: string;
  createdBy: ApiKeyUserSummary | null;
}

/** Returned only from create/rotate — the one and only time the raw key is ever present in a
 * response. Never persist this anywhere beyond the one-time display dialog's own component state. */
export interface ApiKeyCreated extends ApiKeySummary {
  key: string;
}

export interface ServiceAccount {
  id: string;
  name: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export interface CreateServiceAccountRequest {
  name: string;
  role: UserRole;
}

export interface CreateApiKeyRequest {
  userId: string;
  name: string;
  expiresAt: string | null;
}
