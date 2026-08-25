export type CustomFieldType =
  | 'Text'
  | 'Number'
  | 'Currency'
  | 'Date'
  | 'DateTime'
  | 'Checkbox'
  | 'Dropdown'
  | 'MultiSelect'
  | 'User'
  | 'LongText'
  | 'Link';

export interface LinkValue {
  label: string;
  url: string;
}

export interface CustomFieldOption {
  id: string;
  value: string;
  sortOrder: number;
  /** Disabled options stay on tasks that already reference them but can't be picked for a new value. */
  isActive: boolean;
}

export interface CustomField {
  id: string;
  /** Null means this field applies to every project — an Administrator-only capability. */
  projectId: string | null;
  /** Null when projectId is null — rendered as "All Projects" in the admin UI. */
  projectName: string | null;
  name: string;
  description: string | null;
  fieldType: CustomFieldType;
  isRequired: boolean;
  /** Deactivated fields keep their existing task values but can't be given a new one. */
  isActive: boolean;
  defaultValue: string | null;
  sortOrder: number;
  options: CustomFieldOption[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateCustomFieldRequest {
  name: string;
  description?: string;
  fieldType: CustomFieldType;
  isRequired?: boolean;
  defaultValue?: string;
  /** Initial options for Dropdown/MultiSelect fields; ignored for other types. */
  options?: string[];
}

/** Only the Admin > Custom Fields page uses this — everywhere else a field is created via the
 * project-scoped endpoint, which always determines the project from the URL, never the body. */
export interface AdminCreateCustomFieldRequest extends CreateCustomFieldRequest {
  /** Null = applies to every project. */
  projectId: string | null;
}

export interface UpdateCustomFieldRequest {
  name: string;
  description: string | null;
  isRequired: boolean;
  isActive: boolean;
  defaultValue: string | null;
  sortOrder: number;
}

export interface CustomFieldOptionRequest {
  value: string;
  /** Omit to leave unchanged. */
  sortOrder?: number;
  isActive?: boolean;
}

export const CUSTOM_FIELD_TYPE_OPTIONS: CustomFieldType[] = [
  'Text',
  'LongText',
  'Number',
  'Currency',
  'Date',
  'DateTime',
  'Checkbox',
  'Dropdown',
  'MultiSelect',
  'User',
  'Link',
];

export const CUSTOM_FIELD_TYPE_LABELS: Record<CustomFieldType, string> = {
  Text: 'Text',
  LongText: 'Long Text',
  Number: 'Number',
  Currency: 'Currency',
  Date: 'Date',
  DateTime: 'Date/Time',
  Checkbox: 'Checkbox',
  Dropdown: 'Dropdown',
  MultiSelect: 'Multi-select',
  User: 'User',
  Link: 'Link',
};

export function customFieldTypeHasOptions(type: CustomFieldType): boolean {
  return type === 'Dropdown' || type === 'MultiSelect';
}
