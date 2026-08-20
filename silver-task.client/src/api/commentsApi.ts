import { httpClient } from './httpClient';
import type { Comment, CreateCommentRequest, UpdateCommentRequest } from '@/types/comment';

export const commentsApi = {
  list: (taskId: string) => httpClient.get<Comment[]>(`/tasks/${taskId}/comments`),
  create: (taskId: string, request: CreateCommentRequest) => httpClient.post<Comment>(`/tasks/${taskId}/comments`, request),
  update: (id: string, request: UpdateCommentRequest) => httpClient.put<Comment>(`/comments/${id}`, request),
  remove: (id: string) => httpClient.delete<void>(`/comments/${id}`),
};
