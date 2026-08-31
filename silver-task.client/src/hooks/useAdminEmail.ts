import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminEmailApi } from '@/api/adminEmailApi';
import type { UpsertEmailTemplateRequest } from '@/types/email';

const statusKey = ['admin', 'email', 'status'] as const;
const templatesKey = ['admin', 'email', 'templates'] as const;
const deliveriesKey = (page: number, pageSize: number) => ['admin', 'email', 'deliveries', page, pageSize] as const;

export function useEmailStatus() {
  return useQuery({ queryKey: statusKey, queryFn: adminEmailApi.getStatus });
}

export function useSendTestEmail() {
  return useMutation({ mutationFn: (toEmail: string) => adminEmailApi.sendTest(toEmail) });
}

export function useEmailTemplates() {
  return useQuery({ queryKey: templatesKey, queryFn: adminEmailApi.getTemplates });
}

export function useUpsertEmailTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ notificationType, request }: { notificationType: string; request: UpsertEmailTemplateRequest }) =>
      adminEmailApi.upsertTemplate(notificationType, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
  });
}

export function useResetEmailTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (notificationType: string) => adminEmailApi.resetTemplate(notificationType),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
  });
}

export function usePreviewEmailTemplate() {
  return useMutation({ mutationFn: (notificationType: string) => adminEmailApi.previewTemplate(notificationType) });
}

export function useEmailDeliveries(page: number, pageSize: number) {
  return useQuery({
    queryKey: deliveriesKey(page, pageSize),
    queryFn: () => adminEmailApi.getDeliveries(page, pageSize),
  });
}
