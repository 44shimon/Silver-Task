import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiKeysApi } from '@/api/apiKeysApi';
import type { CreateApiKeyRequest, CreateServiceAccountRequest } from '@/types/apiKeys';

export function useServiceAccounts() {
  return useQuery({
    queryKey: ['admin', 'service-accounts'],
    queryFn: apiKeysApi.listServiceAccounts,
  });
}

export function useApiKeys() {
  return useQuery({
    queryKey: ['admin', 'api-keys'],
    queryFn: apiKeysApi.listApiKeys,
  });
}

function useInvalidateApiKeys() {
  const queryClient = useQueryClient();
  return () => {
    queryClient.invalidateQueries({ queryKey: ['admin', 'api-keys'] });
    queryClient.invalidateQueries({ queryKey: ['admin', 'service-accounts'] });
  };
}

export function useCreateServiceAccount() {
  const invalidate = useInvalidateApiKeys();
  return useMutation({
    mutationFn: (request: CreateServiceAccountRequest) => apiKeysApi.createServiceAccount(request),
    onSuccess: invalidate,
  });
}

export function useDeactivateServiceAccount() {
  const invalidate = useInvalidateApiKeys();
  return useMutation({
    mutationFn: (id: string) => apiKeysApi.deactivateServiceAccount(id),
    onSuccess: invalidate,
  });
}

export function useCreateApiKey() {
  const invalidate = useInvalidateApiKeys();
  return useMutation({
    mutationFn: (request: CreateApiKeyRequest) => apiKeysApi.createApiKey(request),
    onSuccess: invalidate,
  });
}

export function useRotateApiKey() {
  const invalidate = useInvalidateApiKeys();
  return useMutation({
    mutationFn: (id: string) => apiKeysApi.rotateApiKey(id),
    onSuccess: invalidate,
  });
}

export function useRevokeApiKey() {
  const invalidate = useInvalidateApiKeys();
  return useMutation({
    mutationFn: (id: string) => apiKeysApi.revokeApiKey(id),
    onSuccess: invalidate,
  });
}
