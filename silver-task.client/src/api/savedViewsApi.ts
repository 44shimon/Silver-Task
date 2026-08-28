import { httpClient } from './httpClient';
import type { ExecuteViewResult, PreviewResult, PreviewViewRequest, SavedView, SaveViewRequest } from '@/types/savedView';

export const savedViewsApi = {
  list: () => httpClient.get<SavedView[]>('/views'),
  getById: (id: string) => httpClient.get<SavedView>(`/views/${id}`),
  create: (request: SaveViewRequest) => httpClient.post<SavedView>('/views', request),
  update: (id: string, request: SaveViewRequest) => httpClient.put<SavedView>(`/views/${id}`, request),
  remove: (id: string) => httpClient.delete<void>(`/views/${id}`),
  duplicate: (id: string) => httpClient.post<SavedView>(`/views/${id}/duplicate`),
  share: (id: string, email: string) => httpClient.post<void>(`/views/${id}/share`, { email }),
  unshare: (id: string, userId: string) => httpClient.delete<void>(`/views/${id}/share/${userId}`),
  favorite: (id: string) => httpClient.post<void>(`/views/${id}/favorite`),
  unfavorite: (id: string) => httpClient.delete<void>(`/views/${id}/favorite`),
  reorderFavorites: (orderedViewIds: string[]) => httpClient.put<void>('/views/favorites/order', orderedViewIds),
  execute: (id: string, page: number, pageSize: number) =>
    httpClient.get<ExecuteViewResult>(`/views/${id}/execute?page=${page}&pageSize=${pageSize}`),
  preview: (request: PreviewViewRequest) => httpClient.post<PreviewResult>('/views/preview', request),
};
