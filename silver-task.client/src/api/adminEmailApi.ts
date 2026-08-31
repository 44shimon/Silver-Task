import { httpClient } from './httpClient';
import type {
  EmailDeliveryPage,
  EmailTemplate,
  EmailTemplatePreview,
  TestEmailResult,
  UpsertEmailTemplateRequest,
} from '@/types/email';

/** Administrator-only — matches AdminEmailController's [Authorize(Roles=Administrator)]. */
export const adminEmailApi = {
  getStatus: () => httpClient.get<{ isConfigured: boolean }>('/admin/email/status'),
  sendTest: (toEmail: string) => httpClient.post<TestEmailResult>('/admin/email/test', { toEmail }),
  getTemplates: () => httpClient.get<EmailTemplate[]>('/admin/email/templates'),
  upsertTemplate: (notificationType: string, request: UpsertEmailTemplateRequest) =>
    httpClient.put<EmailTemplate>(`/admin/email/templates/${notificationType}`, request),
  resetTemplate: (notificationType: string) => httpClient.post<void>(`/admin/email/templates/${notificationType}/reset`),
  previewTemplate: (notificationType: string) =>
    httpClient.post<EmailTemplatePreview>(`/admin/email/templates/${notificationType}/preview`),
  getDeliveries: (page: number, pageSize: number) =>
    httpClient.get<EmailDeliveryPage>(`/admin/email/deliveries?page=${page}&pageSize=${pageSize}`),
};
