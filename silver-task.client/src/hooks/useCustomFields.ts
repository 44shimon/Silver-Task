import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { customFieldsApi } from '@/api/customFieldsApi';
import type { CreateCustomFieldRequest, CustomFieldEntityType, CustomFieldOptionRequest, UpdateCustomFieldRequest } from '@/types/customField';

const customFieldsKey = (projectId: string, entityType: CustomFieldEntityType = 'Task') =>
  ['projects', projectId, 'customFields', entityType] as const;

export function useCustomFields(projectId: string | undefined, entityType: CustomFieldEntityType = 'Task') {
  return useQuery({
    queryKey: customFieldsKey(projectId ?? '', entityType),
    queryFn: () => customFieldsApi.list(projectId!, entityType),
    enabled: Boolean(projectId),
  });
}

export function useCreateCustomField(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateCustomFieldRequest) => customFieldsApi.create(projectId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'customFields'] });
    },
  });
}

export function useReorderCustomFields(projectId: string, entityType: CustomFieldEntityType = 'Task') {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (orderedFieldIds: string[]) => customFieldsApi.reorder(projectId, orderedFieldIds),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: customFieldsKey(projectId, entityType) });
    },
  });
}

export function useUpdateCustomField(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateCustomFieldRequest }) =>
      customFieldsApi.update(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'customFields'] });
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
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'customFields'] });
      // A deleted field's values disappear from tasks/the project too.
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'tasks'] });
      queryClient.invalidateQueries({ queryKey: ['projects', projectId] });
    },
  });
}

export function useAddCustomFieldOption(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ fieldId, value }: { fieldId: string; value: string }) => customFieldsApi.addOption(fieldId, value),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'customFields'] });
    },
  });
}

export function useUpdateCustomFieldOption(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ fieldId, optionId, request }: { fieldId: string; optionId: string; request: CustomFieldOptionRequest }) =>
      customFieldsApi.updateOption(fieldId, optionId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'customFields'] });
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
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'customFields'] });
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'tasks'] });
    },
  });
}

/** Phase 41 — the Project-scope equivalent of useSetTaskCustomValue, following the same
 * optimistic-update-with-rollback shape (CLAUDE.md's own established pattern). */
export function useSetProjectCustomValue(projectId: string) {
  const queryClient = useQueryClient();
  const queryKey = ['projects', projectId] as const;

  return useMutation({
    mutationFn: ({ customFieldId, value }: { customFieldId: string; value: string | null }) =>
      customFieldsApi.setProjectCustomValue(projectId, customFieldId, value),
    onSuccess: (project) => {
      queryClient.setQueryData(queryKey, project);
    },
  });
}
