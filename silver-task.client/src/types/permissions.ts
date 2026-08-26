/** Mirrors Silver-Task.Server/Common/Permissions.cs — plain "Group.Action" strings, kept as
 * named constants here purely for autocomplete/typo-safety in the frontend, not because the
 * frontend independently decides what they mean. Backend PermissionService is the single source
 * of truth for the matrix; this file only needs to agree on the string values. */
export const Permissions = {
  UsersView: 'Users.View',
  UsersCreate: 'Users.Create',
  UsersEdit: 'Users.Edit',
  UsersDelete: 'Users.Delete',
  UsersManageRoles: 'Users.ManageRoles',

  ProjectsView: 'Projects.View',
  ProjectsCreate: 'Projects.Create',
  ProjectsEdit: 'Projects.Edit',
  ProjectsDelete: 'Projects.Delete',
  ProjectsManageMembers: 'Projects.ManageMembers',

  TasksView: 'Tasks.View',
  TasksCreate: 'Tasks.Create',
  TasksEdit: 'Tasks.Edit',
  TasksDelete: 'Tasks.Delete',
  TasksAssign: 'Tasks.Assign',

  CommentsCreate: 'Comments.Create',
  CommentsDelete: 'Comments.Delete',

  FilesUpload: 'Files.Upload',
  FilesDelete: 'Files.Delete',

  DependenciesManage: 'Dependencies.Manage',
  RecurringTasksManage: 'RecurringTasks.Manage',

  CustomFieldsManage: 'CustomFields.Manage',

  ReportsView: 'Reports.View',
  ReportsExport: 'Reports.Export',

  SettingsView: 'Settings.View',
  SettingsEdit: 'Settings.Edit',

  AdministrationAccess: 'Administration.Access',
} as const;

export type PermissionCode = (typeof Permissions)[keyof typeof Permissions];

/** Human-readable label per permission code, for the Admin -> Roles & Permissions matrix. */
export const PERMISSION_LABELS: Record<string, string> = {
  [Permissions.UsersView]: 'View Users',
  [Permissions.UsersCreate]: 'Create Users',
  [Permissions.UsersEdit]: 'Edit Users',
  [Permissions.UsersDelete]: 'Delete Users',
  [Permissions.UsersManageRoles]: 'Manage Roles',

  [Permissions.ProjectsView]: 'View All Projects',
  [Permissions.ProjectsCreate]: 'Create Projects',
  [Permissions.ProjectsEdit]: 'Edit Projects',
  [Permissions.ProjectsDelete]: 'Delete/Archive Projects',
  [Permissions.ProjectsManageMembers]: 'Manage Members',

  [Permissions.TasksView]: 'View Tasks',
  [Permissions.TasksCreate]: 'Create Tasks',
  [Permissions.TasksEdit]: 'Edit Tasks',
  [Permissions.TasksDelete]: 'Delete Tasks',
  [Permissions.TasksAssign]: 'Assign Tasks',

  [Permissions.CommentsCreate]: 'Add Comments',
  [Permissions.CommentsDelete]: 'Delete Any Comment',

  [Permissions.FilesUpload]: 'Upload Files',
  [Permissions.FilesDelete]: 'Delete Any File',

  [Permissions.DependenciesManage]: 'Manage Dependencies',
  [Permissions.RecurringTasksManage]: 'Manage Recurring Tasks',

  [Permissions.CustomFieldsManage]: 'Manage Custom Fields',

  [Permissions.ReportsView]: 'View Reports',
  [Permissions.ReportsExport]: 'Export Reports',

  [Permissions.SettingsView]: 'View System Settings',
  [Permissions.SettingsEdit]: 'Edit System Settings',

  [Permissions.AdministrationAccess]: 'Access Admin Area',
};

/** Group label -> permission codes, same grouping/order as the backend's Permissions.Groups —
 * drives the Admin -> Roles & Permissions matrix so a flat 27-permission list never has to be
 * read/scanned as one undifferentiated block. */
export const PERMISSION_GROUPS: Record<string, string[]> = {
  Users: [Permissions.UsersView, Permissions.UsersCreate, Permissions.UsersEdit, Permissions.UsersDelete, Permissions.UsersManageRoles],
  Projects: [
    Permissions.ProjectsView,
    Permissions.ProjectsCreate,
    Permissions.ProjectsEdit,
    Permissions.ProjectsDelete,
    Permissions.ProjectsManageMembers,
  ],
  Tasks: [Permissions.TasksView, Permissions.TasksCreate, Permissions.TasksEdit, Permissions.TasksDelete, Permissions.TasksAssign],
  Comments: [Permissions.CommentsCreate, Permissions.CommentsDelete],
  Files: [Permissions.FilesUpload, Permissions.FilesDelete],
  'Dependencies & Recurring Tasks': [Permissions.DependenciesManage, Permissions.RecurringTasksManage],
  'Custom Fields': [Permissions.CustomFieldsManage],
  Reports: [Permissions.ReportsView, Permissions.ReportsExport],
  Settings: [Permissions.SettingsView, Permissions.SettingsEdit],
  Administration: [Permissions.AdministrationAccess],
};
