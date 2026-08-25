import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { usersApi } from '@/api/usersApi';
import type { AdminUser, CreateUserRequest, ResetPasswordRequest, UpdateUserRequest } from '@/types/admin';

const usersKey = ['users'] as const;

/** UpdateUserRequest is a full-resource replace (same reasoning as buildBaseRequest for
 * tasks/projects) — every field-level editor builds a full request from the current user
 * plus its own patch, so no editor accidentally clobbers a field it doesn't own. */
export function buildUserUpdateRequest(user: AdminUser, patch: Partial<UpdateUserRequest>): UpdateUserRequest {
  return {
    name: user.name,
    role: user.role,
    isActive: user.isActive,
    ...patch,
  };
}

export function useAdminUsers() {
  return useQuery({
    queryKey: usersKey,
    queryFn: usersApi.list,
  });
}

export function useCreateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateUserRequest) => usersApi.create(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: usersKey });
    },
  });
}

/** Optimistic update + rollback, same shape as useUpdateTask — Role/Active are rendered as
 * live dropdown/toggle cells like Status/Priority, so they should behave the same way. */
export function useUpdateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateUserRequest }) => usersApi.update(id, request),
    onMutate: async ({ id, request }) => {
      await queryClient.cancelQueries({ queryKey: usersKey });
      const previousUsers = queryClient.getQueryData<AdminUser[]>(usersKey);

      queryClient.setQueryData<AdminUser[]>(usersKey, (old) =>
        old?.map((u) => (u.id === id ? { ...u, ...request } : u)),
      );

      return { previousUsers };
    },
    onError: (_error, _variables, context) => {
      if (context?.previousUsers) {
        queryClient.setQueryData(usersKey, context.previousUsers);
      }
    },
    onSuccess: (updatedUser) => {
      queryClient.setQueryData<AdminUser[]>(usersKey, (old) =>
        old?.map((u) => (u.id === updatedUser.id ? updatedUser : u)),
      );
    },
  });
}

export function useResetUserPassword() {
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: ResetPasswordRequest }) =>
      usersApi.resetPassword(id, request),
  });
}

/** Only fetched once the delete-confirmation dialog is actually open (enabled), not eagerly for
 * every row in the table — it's a handful of count queries the backend runs on demand. */
export function useUserDeletionImpact(id: string | undefined) {
  return useQuery({
    queryKey: ['users', id, 'deletion-impact'],
    queryFn: () => usersApi.getDeletionImpact(id!),
    enabled: Boolean(id),
  });
}

export function useDeleteUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => usersApi.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: usersKey });
    },
  });
}
