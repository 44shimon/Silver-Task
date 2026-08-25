import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { customFieldsApi } from '@/api/customFieldsApi';
import type { CreateCustomFieldRequest, CustomFieldOptionRequest, UpdateCustomFieldRequest } from '@/types/customField';

const customFieldsKey = (projectId: string) => ['projects', projectId, 'customFields'] as const;

export function useCustomFields(projectId: string | undefined) {
  return useQuery({
    queryKey: customFieldsKey(projectId ?? ''),
    queryFn: () => customFieldsApi.list(projectId!),
    enabled: Boolean(projectId),
  });
}

export function useCreateCustomField(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateCustomFieldRequest) => customFieldsApi.create(projectId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: customFieldsKey(projectId) });
    },
  });
}

export function useUpdateCustomField(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateCustomFieldRequest }) =>
      customFieldsApi.update(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: customFieldsKey(projectId) });
    },
  });
}

export function useDeleteCustomField(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    // confirm defaults to false — the backend rejects deleting a field that still has task
    // values unless the caller explicitly confirms, so a first attempt can surface that as a
    // 409 the UI turns into a "used by N tasks, delete anyway?" prompt before retrying with true.
    mutationFn: ({ id, confirm }: { id: string; confirm?: boolean }) => customFieldsApi.remove(id, confirm),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: customFieldsKey(projectId) });
      // A deleted field's values disappear from tasks too.
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'tasks'] });
    },
  });
}

export function useAddCustomFieldOption(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ fieldId, value }: { fieldId: string; value: string }) => customFieldsApi.addOption(fieldId, value),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: customFieldsKey(projectId) });
    },
  });
}

export function useUpdateCustomFieldOption(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ fieldId, optionId, request }: { fieldId: string; optionId: string; request: CustomFieldOptionRequest }) =>
      customFieldsApi.updateOption(fieldId, optionId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: customFieldsKey(projectId) });
    },
  });
}

export function useDeleteCustomFieldOption(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    // Same confirm-then-retry shape as useDeleteCustomField, for the same reason.
    mutationFn: ({ fieldId, optionId, confirm }: { fieldId: string; optionId: string; confirm?: boolean }) =>
      customFieldsApi.removeOption(fieldId, optionId, confirm),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: customFieldsKey(projectId) });
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'tasks'] });
    },
  });
}
