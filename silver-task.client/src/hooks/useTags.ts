import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminTagsApi, tagsApi } from '@/api/tagsApi';

export function useActiveTags() {
  return useQuery({
    queryKey: ['tags', 'active'],
    queryFn: tagsApi.listActive,
  });
}

export function useAdminTags() {
  return useQuery({
    queryKey: ['admin', 'tags'],
    queryFn: adminTagsApi.listAll,
  });
}

function useInvalidateTags() {
  const queryClient = useQueryClient();
  return () => {
    queryClient.invalidateQueries({ queryKey: ['admin', 'tags'] });
    queryClient.invalidateQueries({ queryKey: ['tags', 'active'] });
  };
}

export function useRenameTag() {
  const invalidate = useInvalidateTags();
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => adminTagsApi.rename(id, name),
    onSuccess: invalidate,
  });
}

export function useSetTagActive() {
  const invalidate = useInvalidateTags();
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      isActive ? adminTagsApi.activate(id) : adminTagsApi.deactivate(id),
    onSuccess: invalidate,
  });
}

export function useDeleteTag() {
  const invalidate = useInvalidateTags();
  return useMutation({
    mutationFn: (id: string) => adminTagsApi.remove(id),
    onSuccess: invalidate,
  });
}
