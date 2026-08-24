import { httpClient } from './httpClient';
import type { AdminUser, CreateUserRequest, ResetPasswordRequest, UpdateUserRequest } from '@/types/admin';

/** Every endpoint here is Administrator-only on the backend ([Authorize(Roles=Administrator)]
 * on UsersController) — this client-side wrapper adds no authorization of its own. */
export const usersApi = {
  list: () => httpClient.get<AdminUser[]>('/users'),
  create: (request: CreateUserRequest) => httpClient.post<AdminUser>('/users', request),
  update: (id: string, request: UpdateUserRequest) => httpClient.put<AdminUser>(`/users/${id}`, request),
  resetPassword: (id: string, request: ResetPasswordRequest) =>
    httpClient.post<void>(`/users/${id}/reset-password`, request),
};
