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
  | 'Link'
  | 'Url'
  | 'Email'
  | 'Phone'
  | 'UserMulti'
  | 'TaskReference'
  | 'ProjectReference';

/** Phase 41 — which kind of object a field's values attach to. Distinct from `projectId` (which
 * scopes the field *definition* to one project vs. every project — the "Organization" concept in
 * this single-tenant app). */
export type CustomFieldEntityType = 'Task' | 'Project';

/** Mirrors Silver-Task.Server/Models/Entities/Enums/AutomationConditionOperator.cs — reused here
 * (not a second operator enum) for a field's own single-condition visibility rule. */
export type CustomFieldConditionOperator =
  | 'Equals'
  | 'NotEquals'
  | 'Contains'
  | 'NotContains'
  | 'IsEmpty'
  | 'IsNotEmpty'
  | 'GreaterThan'
  | 'LessThan'
  | 'GreaterThanOrEqual'
  | 'LessThanOrEqual'
  | 'Before'
  | 'After';

export const CONDITION_OPERATOR_OPTIONS: CustomFieldConditionOperator[] = [
  'Equals',
  'NotEquals',
  'Contains',
  'NotContains',
  'IsEmpty',
  'IsNotEmpty',
  'GreaterThan',
  'LessThan',
];

export const CONDITION_OPERATOR_LABELS: Record<CustomFieldConditionOperator, string> = {
  Equals: 'Equals',
  NotEquals: 'Not Equals',
  Contains: 'Contains',
  NotContains: 'Does Not Contain',
  IsEmpty: 'Is Empty',
  IsNotEmpty: 'Is Not Empty',
  GreaterThan: 'Greater Than',
  LessThan: 'Less Than',
  GreaterThanOrEqual: 'At Least',
  LessThanOrEqual: 'At Most',
  Before: 'Is Before',
  After: 'Is After',
};

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
  /** Stable, immutable, snake_case internal key generated from name at creation. Never changes
   * when the field is renamed. */
  identifier: string;
  description: string | null;
  fieldType: CustomFieldType;
  entityType: CustomFieldEntityType;
  isRequired: boolean;
  /** Deactivated fields keep their existing task values but can't be given a new one. */
  isActive: boolean;
  defaultValue: string | null;
  sortOrder: number;
  /** Display-only section label — fields sharing the same groupName render together. */
  groupName: string | null;
  placeholder: string | null;
  maxLength: number | null;
  minValue: number | null;
  maxValue: number | null;
  decimalPlaces: number | null;
  /** A private field's value is redacted server-side for anyone not covered by
   * isPrivate/visibleToRoles — never merely hidden client-side. */
  isPrivate: boolean;
  /** Comma-separated UserRole names, e.g. "Administrator,Manager". */
  visibleToRoles: string | null;
  /** Basic single-condition visibility: this field is only shown/required when
   * conditionFieldId's value compares to conditionValue via conditionOperator. */
  conditionFieldId: string | null;
  conditionOperator: CustomFieldConditionOperator | null;
  conditionValue: string | null;
  options: CustomFieldOption[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateCustomFieldRequest {
  name: string;
  description?: string;
  fieldType: CustomFieldType;
  entityType?: CustomFieldEntityType;
  isRequired?: boolean;
  defaultValue?: string;
  /** Initial options for Dropdown/MultiSelect fields; ignored for other types. */
  options?: string[];
  groupName?: string;
  placeholder?: string;
  maxLength?: number;
  minValue?: number;
  maxValue?: number;
  decimalPlaces?: number;
  isPrivate?: boolean;
  visibleToRoles?: string;
  conditionFieldId?: string;
  conditionOperator?: CustomFieldConditionOperator;
  conditionValue?: string;
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
  groupName?: string;
  placeholder?: string;
  maxLength?: number;
  minValue?: number;
  maxValue?: number;
  decimalPlaces?: number;
  isPrivate?: boolean;
  visibleToRoles?: string;
  conditionFieldId?: string;
  conditionOperator?: CustomFieldConditionOperator;
  conditionValue?: string;
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
  'UserMulti',
  'Link',
  'Url',
  'Email',
  'Phone',
  'TaskReference',
  'ProjectReference',
];

export const CUSTOM_FIELD_TYPE_LABELS: Record<CustomFieldType, string> = {
  Text: 'Text',
  LongText: 'Long Text',
  Number: 'Number',
  Currency: 'Currency',
  Date: 'Date',
  DateTime: 'Date/Time',
  Checkbox: 'Yes / No',
  Dropdown: 'Dropdown',
  MultiSelect: 'Multi-select',
  User: 'User',
  UserMulti: 'Users',
  Link: 'Link',
  Url: 'URL',
  Email: 'Email',
  Phone: 'Phone',
  TaskReference: 'Task',
  ProjectReference: 'Project',
};

export function customFieldTypeHasOptions(type: CustomFieldType): boolean {
  return type === 'Dropdown' || type === 'MultiSelect';
}

export function customFieldTypeSupportsMaxLength(type: CustomFieldType): boolean {
  return type === 'Text' || type === 'LongText' || type === 'Url' || type === 'Email' || type === 'Phone';
}

export function customFieldTypeSupportsRange(type: CustomFieldType): boolean {
  return type === 'Number' || type === 'Currency';
}
