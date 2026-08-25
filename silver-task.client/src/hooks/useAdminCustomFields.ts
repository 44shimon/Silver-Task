import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminCustomFieldsApi, type AdminCustomFieldFilters } from '@/api/adminCustomFieldsApi';
import { customFieldsApi } from '@/api/customFieldsApi';
import type { AdminCreateCustomFieldRequest, CustomFieldOptionRequest, UpdateCustomFieldRequest } from '@/types/customField';

const adminCustomFieldsKey = (filters: AdminCustomFieldFilters) => ['admin', 'customFields', filters] as const;
// Broad predicate so a mutation invalidates every filtered variant currently cached, rather
// than tracking down which exact filter combination is mounted right now.
const ADMIN_CUSTOM_FIELDS_ROOT_KEY = ['admin', 'customFields'] as const;

export function useAdminCustomFields(filters: AdminCustomFieldFilters) {
  return useQuery({
    queryKey: adminCustomFieldsKey(filters),
    queryFn: () => adminCustomFieldsApi.getAll(filters),
  });
}

export function useAdminCreateCustomField() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AdminCreateCustomFieldRequest) => adminCustomFieldsApi.create(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ADMIN_CUSTOM_FIELDS_ROOT_KEY });
    },
  });
}

// The remaining mutations reuse customFieldsApi directly — update/delete/options are all
// field-id-keyed REST endpoints CustomFieldService already authorizes per-field regardless of
// which page called in, so there's no need for admin-specific API functions, only admin-specific
// cache invalidation.
export function useAdminUpdateCustomField() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateCustomFieldRequest }) => customFieldsApi.update(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ADMIN_CUSTOM_FIELDS_ROOT_KEY });
    },
  });
}

export function useAdminDeleteCustomField() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, confirm }: { id: string; confirm?: boolean }) => customFieldsApi.remove(id, confirm),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ADMIN_CUSTOM_FIELDS_ROOT_KEY });
    },
  });
}

export function useAdminAddCustomFieldOption() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ fieldId, value }: { fieldId: string; value: string }) => customFieldsApi.addOption(fieldId, value),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ADMIN_CUSTOM_FIELDS_ROOT_KEY });
    },
  });
}

export function useAdminUpdateCustomFieldOption() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ fieldId, optionId, request }: { fieldId: string; optionId: string; request: CustomFieldOptionRequest }) =>
      customFieldsApi.updateOption(fieldId, optionId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ADMIN_CUSTOM_FIELDS_ROOT_KEY });
    },
  });
}

export function useAdminDeleteCustomFieldOption() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ fieldId, optionId, confirm }: { fieldId: string; optionId: string; confirm?: boolean }) =>
      customFieldsApi.removeOption(fieldId, optionId, confirm),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ADMIN_CUSTOM_FIELDS_ROOT_KEY });
    },
  });
}
