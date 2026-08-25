import { httpClient } from './httpClient';
import type {
  CreateCustomFieldRequest,
  CustomField,
  CustomFieldOption,
  CustomFieldOptionRequest,
  UpdateCustomFieldRequest,
} from '@/types/customField';

export const customFieldsApi = {
  list: (projectId: string) => httpClient.get<CustomField[]>(`/projects/${projectId}/custom-fields`),
  create: (projectId: string, request: CreateCustomFieldRequest) =>
    httpClient.post<CustomField>(`/projects/${projectId}/custom-fields`, request),
  update: (id: string, request: UpdateCustomFieldRequest) => httpClient.put<CustomField>(`/custom-fields/${id}`, request),
  // confirm=true is required by the backend to delete a field that already has task values —
  // omit it for a first attempt and retry with confirm once the caller has shown that warning.
  remove: (id: string, confirm = false) => httpClient.delete<void>(`/custom-fields/${id}${confirm ? '?confirm=true' : ''}`),
  usage: (id: string) => httpClient.get<{ taskCount: number }>(`/custom-fields/${id}/usage`),
  addOption: (fieldId: string, value: string) =>
    httpClient.post<CustomFieldOption>(`/custom-fields/${fieldId}/options`, { value }),
  updateOption: (fieldId: string, optionId: string, request: CustomFieldOptionRequest) =>
    httpClient.put<CustomFieldOption>(`/custom-fields/${fieldId}/options/${optionId}`, request),
  removeOption: (fieldId: string, optionId: string, confirm = false) =>
    httpClient.delete<void>(`/custom-fields/${fieldId}/options/${optionId}${confirm ? '?confirm=true' : ''}`),
};
