import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { savedViewsApi } from '@/api/savedViewsApi';
import type { PreviewViewRequest, SaveViewRequest } from '@/types/savedView';

const savedViewsKey = ['saved-views'] as const;

export function useSavedViews() {
  return useQuery({ queryKey: savedViewsKey, queryFn: savedViewsApi.list });
}

export function useSavedView(id: string | undefined) {
  return useQuery({
    queryKey: [...savedViewsKey, id],
    queryFn: () => savedViewsApi.getById(id!),
    enabled: !!id,
  });
}

export function useCreateSavedView() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SaveViewRequest) => savedViewsApi.create(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedViewsKey }),
  });
}

export function useUpdateSavedView(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SaveViewRequest) => savedViewsApi.update(id, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedViewsKey }),
  });
}

export function useDeleteSavedView() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => savedViewsApi.remove(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedViewsKey }),
  });
}

export function useDuplicateSavedView() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => savedViewsApi.duplicate(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedViewsKey }),
  });
}

export function useShareSavedView() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, email }: { id: string; email: string }) => savedViewsApi.share(id, email),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedViewsKey }),
  });
}

export function useUnshareSavedView() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, userId }: { id: string; userId: string }) => savedViewsApi.unshare(id, userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedViewsKey }),
  });
}

export function useToggleSavedViewFavorite() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, favorite }: { id: string; favorite: boolean }) =>
      favorite ? savedViewsApi.favorite(id) : savedViewsApi.unfavorite(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedViewsKey }),
  });
}

export function useReorderSavedViewFavorites() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (orderedViewIds: string[]) => savedViewsApi.reorderFavorites(orderedViewIds),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedViewsKey }),
  });
}

export function useExecuteSavedView(id: string | undefined, page: number, pageSize: number) {
  return useQuery({
    queryKey: [...savedViewsKey, id, 'execute', page, pageSize],
    queryFn: () => savedViewsApi.execute(id!, page, pageSize),
    enabled: !!id,
  });
}

/** Debounced by the caller (the filter builder), never fired on every keystroke — backs the
 * lightweight "N matching tasks" live preview while a view is being built/edited. */
export function usePreviewSavedView(request: PreviewViewRequest | null) {
  return useQuery({
    queryKey: ['saved-view-preview', request],
    queryFn: () => savedViewsApi.preview(request!),
    enabled: request !== null,
  });
}
