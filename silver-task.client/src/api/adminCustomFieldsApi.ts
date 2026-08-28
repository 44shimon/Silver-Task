import { httpClient } from './httpClient';
import type { AdminCreateCustomFieldRequest, CustomField, CustomFieldEntityType, CustomFieldType } from '@/types/customField';

export interface AdminCustomFieldFilters {
  projectId?: string;
  fieldType?: CustomFieldType;
  entityType?: CustomFieldEntityType;
  isActive?: boolean;
}

function buildQuery(filters: AdminCustomFieldFilters): string {
  const params = new URLSearchParams();
  if (filters.projectId) params.set('projectId', filters.projectId);
  if (filters.fieldType) params.set('fieldType', filters.fieldType);
  if (filters.entityType) params.set('entityType', filters.entityType);
  if (filters.isActive !== undefined) params.set('isActive', String(filters.isActive));
  const query = params.toString();
  return query ? `?${query}` : '';
}

export const adminCustomFieldsApi = {
  getAll: (filters: AdminCustomFieldFilters) => httpClient.get<CustomField[]>(`/admin/custom-fields${buildQuery(filters)}`),
  create: (request: AdminCreateCustomFieldRequest) => httpClient.post<CustomField>('/admin/custom-fields', request),
  reorder: (orderedFieldIds: string[]) => httpClient.post<void>('/admin/custom-fields/reorder', orderedFieldIds),
};
