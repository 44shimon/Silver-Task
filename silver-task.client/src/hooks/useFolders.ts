import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { foldersApi } from '@/api/foldersApi';
import type { CreateFolderRequest, FolderDeleteMode } from '@/types/folder';

const foldersKey = (projectId: string, includeDeleted = false) => ['projects', projectId, 'folders', includeDeleted] as const;

export function useFolders(projectId: string, includeDeleted = false) {
  return useQuery({
    queryKey: foldersKey(projectId, includeDeleted),
    queryFn: () => foldersApi.listForProject(projectId, includeDeleted),
  });
}

export function useCreateFolder(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateFolderRequest) => foldersApi.create(projectId, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'folders'] }),
  });
}

export function useRenameFolder(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => foldersApi.rename(id, name),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'folders'] }),
  });
}

export function useMoveFolder(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, parentFolderId }: { id: string; parentFolderId: string | null }) => foldersApi.move(id, parentFolderId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'folders'] }),
  });
}

export function useFolderDeletePreview(id: string | null) {
  return useQuery({
    queryKey: ['folders', id, 'delete-preview'],
    queryFn: () => foldersApi.getDeletePreview(id!),
    enabled: !!id,
  });
}

export function useDeleteFolder(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, mode }: { id: string; mode: FolderDeleteMode }) => foldersApi.remove(id, mode),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'folders'] });
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'files'] });
    },
  });
}

export function useRestoreFolder(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => foldersApi.restore(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'folders'] }),
  });
}
