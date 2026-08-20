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
  | 'LongText';

export interface CustomFieldOption {
  id: string;
  value: string;
  sortOrder: number;
}

export interface CustomField {
  id: string;
  projectId: string;
  name: string;
  fieldType: CustomFieldType;
  sortOrder: number;
  options: CustomFieldOption[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateCustomFieldRequest {
  name: string;
  fieldType: CustomFieldType;
  options?: string[];
}

export interface UpdateCustomFieldRequest {
  name: string;
  sortOrder: number;
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
};

export function customFieldTypeHasOptions(type: CustomFieldType): boolean {
  return type === 'Dropdown' || type === 'MultiSelect';
}
