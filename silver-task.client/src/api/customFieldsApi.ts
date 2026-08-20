import { httpClient } from './httpClient';
import type {
  CreateCustomFieldRequest,
  CustomField,
  CustomFieldOption,
  UpdateCustomFieldRequest,
} from '@/types/customField';

export const customFieldsApi = {
  list: (projectId: string) => httpClient.get<CustomField[]>(`/projects/${projectId}/custom-fields`),
  create: (projectId: string, request: CreateCustomFieldRequest) =>
    httpClient.post<CustomField>(`/projects/${projectId}/custom-fields`, request),
  update: (id: string, request: UpdateCustomFieldRequest) => httpClient.put<CustomField>(`/custom-fields/${id}`, request),
  remove: (id: string) => httpClient.delete<void>(`/custom-fields/${id}`),
  addOption: (fieldId: string, value: string) =>
    httpClient.post<CustomFieldOption>(`/custom-fields/${fieldId}/options`, { value }),
  updateOption: (fieldId: string, optionId: string, value: string) =>
    httpClient.put<CustomFieldOption>(`/custom-fields/${fieldId}/options/${optionId}`, { value }),
  removeOption: (fieldId: string, optionId: string) =>
    httpClient.delete<void>(`/custom-fields/${fieldId}/options/${optionId}`),
};
