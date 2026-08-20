import { httpClient } from './httpClient';
import type { CurrentUser, LoginRequest } from '@/types/auth';

export const authApi = {
  login: (request: LoginRequest) => httpClient.post<CurrentUser>('/auth/login', request),
  logout: () => httpClient.post<void>('/auth/logout'),
  me: () => httpClient.get<CurrentUser>('/auth/me'),
};
