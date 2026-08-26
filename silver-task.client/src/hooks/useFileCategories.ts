import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminFileCategoriesApi, fileCategoriesApi } from '@/api/fileCategoriesApi';

export function useActiveFileCategories() {
  return useQuery({
    queryKey: ['file-categories', 'active'],
    queryFn: fileCategoriesApi.listActive,
  });
}

export function useAdminFileCategories() {
  return useQuery({
    queryKey: ['admin', 'file-categories'],
    queryFn: adminFileCategoriesApi.listAll,
  });
}

function useInvalidateFileCategories() {
  const queryClient = useQueryClient();
  return () => {
    queryClient.invalidateQueries({ queryKey: ['admin', 'file-categories'] });
    queryClient.invalidateQueries({ queryKey: ['file-categories', 'active'] });
  };
}

export function useCreateFileCategory() {
  const invalidate = useInvalidateFileCategories();
  return useMutation({
    mutationFn: ({ name, description }: { name: string; description?: string }) =>
      adminFileCategoriesApi.create(name, description),
    onSuccess: invalidate,
  });
}

export function useUpdateFileCategory() {
  const invalidate = useInvalidateFileCategories();
  return useMutation({
    mutationFn: ({ id, name, description }: { id: string; name: string; description?: string }) =>
      adminFileCategoriesApi.update(id, name, description),
    onSuccess: invalidate,
  });
}

export function useSetFileCategoryActive() {
  const invalidate = useInvalidateFileCategories();
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      isActive ? adminFileCategoriesApi.activate(id) : adminFileCategoriesApi.deactivate(id),
    onSuccess: invalidate,
  });
}

export function useDeleteFileCategory() {
  const invalidate = useInvalidateFileCategories();
  return useMutation({
    mutationFn: (id: string) => adminFileCategoriesApi.remove(id),
    onSuccess: invalidate,
  });
}
