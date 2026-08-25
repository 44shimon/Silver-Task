import { httpClient } from './httpClient';
import type { AdminUser, CreateUserRequest, ResetPasswordRequest, UpdateUserRequest, UserDeletionImpact } from '@/types/admin';

/** Every endpoint here is Administrator-only on the backend ([Authorize(Roles=Administrator)]
 * on UsersController/AdminController) — this client-side wrapper adds no authorization of its own. */
export const usersApi = {
  list: () => httpClient.get<AdminUser[]>('/users'),
  create: (request: CreateUserRequest) => httpClient.post<AdminUser>('/users', request),
  update: (id: string, request: UpdateUserRequest) => httpClient.put<AdminUser>(`/users/${id}`, request),
  resetPassword: (id: string, request: ResetPasswordRequest) =>
    httpClient.post<void>(`/users/${id}/reset-password`, request),
  // These two live under /admin/users rather than /users — AdminController groups admin-only
  // concerns (cross-entity counts, permanent deletion) that don't cleanly belong on the
  // resource's own CRUD controller.
  getDeletionImpact: (id: string) => httpClient.get<UserDeletionImpact>(`/admin/users/${id}/deletion-impact`),
  remove: (id: string) => httpClient.delete<void>(`/admin/users/${id}`),
};
