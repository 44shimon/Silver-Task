import { httpClient } from './httpClient';
import type {
  ApiKeyCreated,
  ApiKeySummary,
  CreateApiKeyRequest,
  CreateServiceAccountRequest,
  ServiceAccount,
} from '@/types/apiKeys';

export const apiKeysApi = {
  listServiceAccounts: () => httpClient.get<ServiceAccount[]>('/admin/service-accounts'),
  createServiceAccount: (request: CreateServiceAccountRequest) =>
    httpClient.post<ServiceAccount>('/admin/service-accounts', request),
  deactivateServiceAccount: (id: string) => httpClient.delete<void>(`/admin/service-accounts/${id}`),

  listApiKeys: () => httpClient.get<ApiKeySummary[]>('/admin/api-keys'),
  createApiKey: (request: CreateApiKeyRequest) => httpClient.post<ApiKeyCreated>('/admin/api-keys', request),
  rotateApiKey: (id: string) => httpClient.post<ApiKeyCreated>(`/admin/api-keys/${id}/rotate`),
  revokeApiKey: (id: string) => httpClient.delete<void>(`/admin/api-keys/${id}`),
};
