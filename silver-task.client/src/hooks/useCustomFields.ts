import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { customFieldsApi } from '@/api/customFieldsApi';
import type { CreateCustomFieldRequest, UpdateCustomFieldRequest } from '@/types/customField';

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
    mutationFn: (id: string) => customFieldsApi.remove(id),
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
    mutationFn: ({ fieldId, optionId, value }: { fieldId: string; optionId: string; value: string }) =>
      customFieldsApi.updateOption(fieldId, optionId, value),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: customFieldsKey(projectId) });
    },
  });
}

export function useDeleteCustomFieldOption(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ fieldId, optionId }: { fieldId: string; optionId: string }) =>
      customFieldsApi.removeOption(fieldId, optionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: customFieldsKey(projectId) });
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'tasks'] });
    },
  });
}
