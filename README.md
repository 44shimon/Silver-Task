# Silver-Task

A production-oriented, spreadsheet-style task management application. Projects contain tasks displayed
in an editable, sortable, filterable grid (rows = tasks, columns = fields, including project-defined
custom fields), backed by a real REST API and a relational database.

> **Status:** v1.0.1 released (Phases 1–49 complete — task management, all five views, search/filters/
> saved views, custom fields, templates, comments/files/activity history, automations, dashboards,
> reports, in-app + email notifications with Daily/Weekly digests, admin/system settings, and a
> post-release stabilization pass). See [Development phases](#development-phases) for the full
> phase-by-phase history, and [Email notifications](#email-notifications-phase-45),
> [Notification digests](#notification-digests-phase-46), and
> [V1.0.0 release readiness](#v100-release-readiness-phase-47) below for the most recent architecture.
> Performance work at real scale and an automated test suite remain the two biggest open gaps — see
> `RELEASE_NOTES.md`'s "Known limitations".

## Technology stack

**Frontend** (`silver-task.client/`)
- React 19 + TypeScript
- Vite
- TanStack Table (spreadsheet grid)
- TanStack Query (server state)
- React Router
- Lucide React (icons)

**Backend** (`Silver-Task.Server/`)
- ASP.NET Core (.NET 10) Web API
- Entity Framework Core
- PostgreSQL
- ASP.NET Core authentication/authorization

**Database**
- PostgreSQL

## Architecture

The frontend and backend are separate projects that communicate exclusively over a REST API — the React
app never talks to the database directly. Backend concerns are layered into controllers, DTOs/models,
and middleware; frontend concerns are layered into a centralized API client, React Query hooks, and
presentational components.

```
Silver-Task/
├─ Silver-Task.Server/        ASP.NET Core Web API
│  ├─ Controllers/            HTTP endpoints (thin; no business logic)
│  ├─ Services/                Business logic (auth, users, projects, tasks, custom fields, JWT issuance), one interface + impl per file
│  ├─ Models/Entities/        EF Core entities + enums (Status, Priority, Role, CustomFieldType)
│  ├─ Models/DTOs/             Request/response shapes exposed by the API (never raw entities)
│  ├─ Data/                   AppDbContext, Fluent API configurations, EF Core migrations
│  ├─ Middleware/             Cross-cutting concerns (exception handling, etc.)
│  ├─ Common/                  Shared helpers (claims extensions, auth cookie constants, domain exceptions)
│  ├─ Program.cs              App startup, DI, middleware pipeline
│  └─ appsettings*.json       Environment configuration
├─ silver-task.client/        React + TypeScript SPA
│  └─ src/
│     ├─ api/                 Centralized API client (fetch wrapper + per-resource services)
│     ├─ components/layout/   App shell (topbar, sidebar)
│     ├─ components/auth/      Route guard (RequireAuth)
│     ├─ components/project/   View tabs (Table/Kanban/Calendar/Timeline/Gantt stub)
│     ├─ components/spreadsheet/ TaskTable (TanStack Table) + cell editors + TaskDetailPanel + comments/activity/attachments
│     ├─ hooks/                React Query hooks (incl. auth)
│     ├─ pages/                Route-level views (Dashboard, Login, Project)
│     ├─ providers/            App-wide providers (React Query, etc.)
│     ├─ routes/                React Router route definitions
│     └─ types/                 Shared TypeScript types
└─ Silver-Task.slnx           Visual Studio solution
```

In development, the ASP.NET Core project auto-launches the Vite dev server (via `Microsoft.AspNetCore.SpaProxy`)
and Vite proxies `/api/*` requests to the backend, so the browser only ever talks to one origin. In
production, the ASP.NET Core app serves the compiled SPA as static files and exposes the API from the
same host.

### Database schema

Entity Framework Core (Npgsql provider) maps the following tables, configured via `IEntityTypeConfiguration<T>`
classes in `Data/Configurations/` (see `Data/AppDbContext.cs`):

| Table | Purpose |
|---|---|
| `Users` | Accounts. Global `Role` (Administrator/Manager/Member), hashed passwords, `IsActive` for soft deactivation. |
| `Projects` | Owned by a `User`; `IsArchived`/`ArchivedAt` for archiving instead of hard delete. |
| `ProjectMembers` | Join table granting a user access to a project (unique per project+user). |
| `Tasks` | The spreadsheet rows. Status/Priority are fixed enums stored as text; `SortOrder` is a fractional index for drag-reordering without renumbering siblings. |
| `TaskComments` | Threaded comments on a task. |
| `TaskActivities` | Append-only audit log (`Action`, `FieldName`, `OldValue`, `NewValue`) — survives the acting user being deleted. |
| `TaskAttachments` | File metadata (name, size, MIME type, storage path); no object storage wired up yet. |
| `CustomFields` | Project-defined columns (Text, Number, Currency, Date, Checkbox, Dropdown, MultiSelect, User, LongText, ...). |
| `CustomFieldOptions` | Selectable options for Dropdown/MultiSelect custom fields. |
| `TaskCustomValues` | EAV-style value storage: one row per (task, custom field), so adding a custom field never requires a schema migration. |

Notes:
- All primary keys are `uuid` (`Guid`), which lets the client generate an id for a new row and render it
  optimistically before the API confirms it — needed for the "edit a cell, update UI immediately" spreadsheet
  behavior in later phases.
- The task entity/table is named `TaskItem`/`Tasks` (not `Task`) to avoid colliding with `System.Threading.Tasks.Task`,
  which is implicitly in scope everywhere via `ImplicitUsings`.
- Enums (`Status`, `Priority`, `Role`, custom field `FieldType`) are stored as `varchar`, not native Postgres
  enum types, so adding a new value later is a plain data migration instead of an `ALTER TYPE`.

### Authentication

- Passwords are hashed with ASP.NET Core's `PasswordHasher<User>` (PBKDF2) — never stored or logged in
  plain text.
- On login, the API issues a JWT and sets it as an **httpOnly, Secure, SameSite=Strict cookie**
  (`silvertask_auth`) rather than returning it in the response body. This keeps the token out of reach of
  JavaScript (so an XSS bug can't exfiltrate it) and, combined with `SameSite=Strict` on an app that's
  always same-origin (dev via the SPA proxy, production served from one host), mitigates CSRF without a
  separate token scheme.
- `GET /api/auth/me` is how the frontend discovers whether it's logged in (the cookie itself is invisible
  to JS). `RequireAuth` in the client wraps all app routes and redirects to `/login` when this 401s.
- Authorization is secure-by-default: `Program.cs` sets a `FallbackPolicy` requiring authentication on any
  endpoint without explicit `[Authorize]`/`[AllowAnonymous]`, so a future controller can't accidentally
  ship unauthenticated. Role checks use `[Authorize(Roles = "Administrator")]` etc.
- **Bootstrap:** `POST /api/users` is open only while the `Users` table is empty — that first account is
  always created as Administrator. Once any user exists, the endpoint requires an authenticated
  Administrator. This avoids needing separate seed-data machinery just to get an initial admin into the
  system.
- Failed login attempts are logged (email + reason, never the password) via `AuthService`.

### Projects & authorization model

- Anyone authenticated can create a project; the creator becomes its `Owner` and is automatically added
  as a `ProjectMember` too (so "is a project member" checks and member listings both just work for them).
- **View access** (`GET /api/projects/{id}`, its members): Administrators, the project owner, or any
  project member.
- **Manage access** (rename, add/remove members, archive, delete tasks): Administrators, the project
  owner, or a `Manager` who is a member of that specific project. Plain `Member`s can never manage a
  project, and a `Manager` who isn't a member of a given project can't see or touch it (or its tasks)
  either. This is enforced in `ProjectAccessService` (`EnsureCanParticipateAsync`/`EnsureCanManageAsync`),
  shared by both `ProjectService` and `TaskService` so the two can't drift out of sync — not just at the
  controller/attribute level, since it depends on runtime membership, not just role.
- The owner can never be removed via the members endpoint (`ConflictException` → 409) — ownership
  transfer isn't implemented yet.
- `DELETE /api/projects/{id}` archives (`IsArchived`/`ArchivedAt`) rather than deleting the row. Archived
  projects are excluded from `GET /api/projects` but remain directly viewable by id.
- Members are added **by email**, not by browsing a user directory — `POST /api/projects/{id}/members`
  looks the user up server-side. This is deliberate: `GET /api/users` is Administrator-only, so a
  non-admin project owner/manager still needs a way to add teammates without needing that broader
  permission.

### Tasks

- `GET/POST /api/projects/{id}/tasks` and `GET/PUT/DELETE /api/tasks/{id}` (plus `POST /api/tasks/{id}/duplicate`).
  `PUT` is a full-resource replace (title/description/status/priority/assignee/dates/`sortOrder` all travel
  together), matching the pattern used for Projects/Users.
- **Authorization tier reuses the project model:** viewing, creating, and editing tasks only requires being
  a project participant (Administrator, owner, or any member — including plain `Member`s, matching "Members
  can create/edit tasks"). **Deleting** a task requires the stricter manage tier (Administrator, owner, or a
  `Manager` who is a member — matching "Managers can manage tasks"), so a plain Member can add and edit
  tasks but not delete them.
- Assigning a task validates that `assignedToUserId` is actually a project member (400 `ValidationException`
  if not) — keeps the assignee dropdown's data honest without needing a DB-level constraint.
- `CompletedAt` is managed automatically: transitioning `Status` to `Complete` stamps it with the current
  time; transitioning away from `Complete` clears it. Callers never set it directly.
- `SortOrder` is a fractional index (`double`). New tasks append at `max(SortOrder) + 1`; duplicating a task
  inserts it immediately after the original via the midpoint between it and the next task, rather than
  always landing at the bottom of the list. The client is expected to compute similar midpoints when
  implementing drag-to-reorder (Phase 7) — no server-side renumbering is needed.
- Deleting a task is a hard delete (unlike Projects). Its comments/activity/attachments/custom values
  cascade-delete with it at the database level (configured in Phase 2).

### Spreadsheet UI

- Each project's Table view (`ProjectPage` → `TaskTable`) renders tasks with TanStack Table: sticky
  header, horizontal scroll for wide column sets, and drag-to-resize columns (title/status/priority/
  assigned-to/start-date/due-date + a row-actions column).
- **TanStack Table version note:** the installed `@tanstack/react-table` (v9) replaced the familiar
  `useReactTable` with a new modular `features`-based `useTable` API (opt-in feature slots, tree-shaking).
  `TaskTable` deliberately uses `useLegacyTable` instead — the officially bundled v8-compatible layer
  (`@tanstack/react-table/legacy`), which still includes column resizing (part of `StockFeatures`). This
  is a considered choice, not a leftover from a migration: the classic API is far simpler and lower-risk
  for a display grid that doesn't need v9-only capabilities. Revisit if a future phase needs something
  only the new `features` system provides.
- Status/Priority render as colored badges (`StatusBadge`/`PriorityBadge`); new `--info`/`--warning` CSS
  tokens were added alongside the existing `--accent`/`--success`/`--danger` for this.
- **View architecture:** `ProjectViewTabs` renders Table (active) plus Kanban/Calendar/Timeline/Gantt as
  visibly-present-but-disabled tabs, satisfying the "design for additional views later" requirement without
  building anything beyond Table yet.
- "+ New Task" uses the same inline-form interaction as the sidebar's "+ New Project" (Phase 4) for
  consistency; it intentionally stays open after a successful create so adding several tasks in a row
  doesn't require reopening it.
- Row actions (duplicate/delete) call the Phase 5 API directly.
- Project member management (Phase 4) was moved into a collapsed `<details>` section below the grid so the
  spreadsheet is the visually dominant element, per the app's intended layout.

### Inline editing

- Title, Start Date, and Due Date are click-to-edit (`EditableTitleCell`/`EditableDateCell`). Status,
  Priority, and Assigned To stay as read-only badges/text for now — their dropdown editors are Phase 8.
- **Interaction model:** click a cell (or focus it via Tab and press Enter/Space) to open its editor.
  `Enter` blurs the input, which is the single commit path — so `Enter` and `Tab`/click-away both commit
  through the same code. `Escape` exits edit mode directly without blurring, so it never triggers a commit
  (this relies on React not re-invoking `onBlur` when the input is unmounted via a state change in the same
  handler — a common, deliberate pattern, also used for the Phase 4 project rename/description fields).
  Cells are plain focusable elements (`tabIndex={0}`), not a custom grid controller, so `Tab`/`Shift+Tab`
  move through them via native browser tab order — including through the row-action buttons, which is
  intentional (keyboard users can reach Duplicate/Delete the same way).
- **Optimistic update + rollback**, per the spec's required flow: `useUpdateTask` (`useTasks.ts`) patches
  the React Query cache immediately in `onMutate` (UI updates before the network call resolves), snapshots
  the prior state, sends the request, and on failure restores that snapshot in `onError` — so the cell
  visibly reverts. `onSuccess` reconciles the row with the server's response rather than refetching the
  whole list. A failed edit leaves the cell with a red outline (`editable-cell--error`) until the next edit
  attempt.
- The backend's `PUT /api/tasks/{id}` is a full-resource replace (Phase 5), so a single-field edit still
  needs every other current value. `buildBaseRequest` (`useTasks.ts`) fills in the unchanged fields; each
  editor supplies just its own field via the `taskFieldChange` helper map.

### Dropdown columns

- Status and Priority are always-rendered native `<select>` elements styled to look like the existing
  colored badges (`appearance: none` + per-value background/text color + a Lucide `ChevronDown` overlay),
  not a click-to-open-then-select two-step. Unlike the free-text cells, choosing an option is inherently a
  single atomic action, so there's no draft state, no `isEditing` toggle, and no Escape-to-cancel needed —
  selecting commits immediately via the same `useUpdateTask` optimistic-update/rollback path as Phase 7.
- Assigned To is a dropdown populated from **the project's members**, not every system user — `TaskTable`
  receives a `members: UserSummary[]` prop from `ProjectPage` (mapped from the same `useProjectMembers`
  data the Phase 4 member list already uses), so there's one source of truth for "who's on this project."
  Includes an explicit "Unassigned" option to clear the assignment.
- `taskFieldChange` (`useTasks.ts`) had to grow beyond Phase 7's simple field-name matching once assignee
  editing arrived: the optimistic cache patch needs a `UserSummary` object (`assignedTo`) but the API
  request needs a bare id (`assignedToUserId`). Each field now has its own `{ optimistic, request }`
  constructor so the two shapes never have to coincide.
- The backend still enforces "assignee must be a project member" (Phase 5's `ValidationException` → 400)
  even though the dropdown only ever offers valid members — the UI can't be trusted to always be in sync
  with server-side membership state (e.g. two tabs open, one removing a member while the other still shows
  the stale option list).

### Filtering, sorting & search

- **Fully client-side** (`useTaskFilters.ts`): the project task list has no pagination yet (Phase 14), so
  there's nothing to gain from round-tripping to the server on every keystroke or filter change — search,
  filters, and sort all run as `useMemo` array operations over the already-loaded task list.
- **Search** matches title, description, or any Text/LongText custom field value, case-insensitive
  substring (per spec).
- **Filters** (`TaskFilterPanel`) are AND-combined, per the spec's example (Status = In Progress AND
  Assigned To = Shimon AND Priority = High): Status, Priority, Assigned To (including an explicit
  "Unassigned" option), and Due Date before a given date. Deliberately a fixed set of fields rather than a
  generic field/operator filter-builder — covers every example in the spec without the extra complexity of
  dynamic operator/value-type switching; can be generalized later if custom fields need it.
- **Sort** covers all 7 fields the spec lists — Task, Assigned To, Status, Priority, Due Date, Created
  Date, Updated Date — via the toolbar's Sort menu (`TaskSortMenu`). Created/Updated Date aren't rendered
  grid columns, so the Sort menu is the only way to sort by them. The five sortable columns also have
  clickable headers (`SortableColumnHeader`) as a convenience shortcut that drives the exact same sort
  state, not a second competing mechanism — clicking a header and picking the same field in the Sort menu
  are interchangeable. Clicking the active column's header again (or re-picking its field) toggles
  ascending/descending, matching common spreadsheet behavior.
- Status/Priority sort by a fixed severity rank (`NotStarted < InProgress < ... `, `Low < Medium < High <
  Urgent`), not alphabetically — alphabetical would put "Blocked" before "Complete", which isn't a
  meaningful ordering for a status.
- Date fields (`DateOnly` and ISO timestamp strings) sort correctly via plain string comparison — both
  formats are zero-padded and big-endian, so lexicographic order already matches chronological order; no
  `Date` parsing needed. `dueDate` nulls sort last regardless of direction.
- The empty state distinguishes "no tasks yet" from "no tasks match your search/filters" so an aggressive
  filter doesn't look like the project has no tasks at all.

### Custom fields

The `CustomFields`/`CustomFieldOptions`/`TaskCustomValues` tables (EAV pattern) were built in Phase 2
specifically for this — no migration was needed for this phase, only the API and UI on top of them.

- **Authorization:** managing field *definitions* (create/rename/delete a field, manage its options) uses
  the same manage tier as renaming a project or deleting a task (Administrator, project owner, or a Manager
  who's a member); *setting a value* on a task uses the participate tier, same as every other task edit —
  any project member can fill in a custom field, matching how they can already edit Title/Status/etc.
- **`FieldType` is immutable after creation.** Changing Number to Date after values exist would leave those
  values impossible to interpret; renaming and reordering (`SortOrder`) are the only things `PUT
  /api/custom-fields/{id}` allows.
- **Value storage & validation:** every value is stored as text in `TaskCustomValues.Value` and
  validated/normalized per `FieldType` in `TaskService.ValidateAndNormalizeCustomValueAsync` — Number/
  Currency must parse as `decimal`, Date/DateTime must parse, Checkbox must be exactly `"true"`/`"false"`,
  Dropdown must match one of the field's option ids, MultiSelect must be a JSON array of valid option ids,
  User must be an existing **project member** (same rule as task assignment), and Link must be JSON
  `{"label":"...","url":"..."}` with an absolute `http`/`https` URL — other schemes (e.g. `javascript:`)
  are rejected, which also closes off a potential XSS vector via a malicious link value. Dropdown/
  MultiSelect store option **ids**, not raw text, so renaming an option doesn't orphan existing task
  values.
- **Deleting an option cleans up after itself:** any `TaskCustomValues` referencing a deleted option
  (Dropdown exact match, or MultiSelect's JSON array containing it) are removed rather than left pointing
  at something that no longer exists. Deleting a field cascades to its options and all task values at the
  database level (Phase 2).
- **Duplicating a task copies its custom values** too, consistent with "duplicate" copying everything else
  about a task (Phase 5).
- **Frontend cell editors** (`components/spreadsheet/*CustomValueCell.tsx`) reuse the interaction patterns
  from Phases 7–8 rather than inventing new ones: `TextCustomValueCell` (Text/LongText/Number/Currency) and
  `DateCustomValueCell` (Date/DateTime) are click-to-edit like `EditableTitleCell`; `SelectCustomValueCell`
  (Dropdown/User) is an always-rendered `<select>` like `StatusDropdownCell`; `CheckboxCustomValueCell` is a
  live checkbox; `MultiSelectCustomValueCell` is a `<details>`-based checklist popover, since none of the
  existing editors support multiple selections. All go through `useSetTaskCustomValue`, the same
  optimistic-update/rollback shape as `useUpdateTask`.
- **Link** (added after the initial Phase 10 pass, per feedback) stores a label + URL pair as JSON in the
  same value column — no schema change needed, since `FieldType` is a plain string column specifically so
  new types wouldn't require a migration (Phase 2 design decision, paid off here for real). Displays as a
  clickable button (external-link icon + label, or the hostname if no label was given); editing opens a
  small two-field popover (`LinkCustomValueCell`) rather than an inline single input, since a single
  click-to-edit text field can't cleanly hold two independent values.
- **`CustomFieldsPanel`** (toolbar) is where fields get created and managed: name + type + (for Dropdown/
  MultiSelect) an initial option list on creation, plus inline rename/add/remove for each existing field's
  options. Deliberately no drag-to-reorder UI for fields or options in this phase, consistent with skipping
  full column drag-reorder in Phase 6 — `SortOrder` is there and settable via the API if that's added later.

### Task detail panel

- **Interaction design note:** the spec says "clicking a task" opens the panel, but clicking a task's
  cells already does inline editing (Phases 7–8) — overloading that would make Title/Status/etc. ambiguous
  between "edit this field" and "open the task." Instead there's a dedicated expand icon
  (`Maximize2`) as the grid's leading column, the same pattern Airtable/Notion/Linear use for exactly this
  conflict. Cell clicks still edit in place; the icon is the unambiguous way to open the full task.
- **`?task=<id>` query parameter** (via `useSearchParams`) drives whether the panel is open, not local
  component state — makes a task directly linkable/bookmarkable and means the browser back button closes
  it. The selected task is looked up from the already-loaded task list (`useTasks`), so opening the panel
  doesn't cost a network request.
- A right-side drawer over a backdrop (click backdrop, press Escape, or the close button to dismiss) — a
  drawer rather than a centered modal specifically because the spec requires "the spreadsheet should
  remain visible behind the panel," and a drawer leaves most of the grid visible while a full modal
  wouldn't.
- **Maximum reuse, minimal new code:** Status/Priority/Assigned To/Start Date/Due Date/Custom Fields in the
  panel are the *exact same* `StatusDropdownCell`/`PriorityDropdownCell`/`AssignedToDropdownCell`/
  `EditableDateCell`/`CustomFieldCell` components already used in the grid — none of them were ever coupled
  to being inside a `<td>`, so they work unmodified in a form layout. Only Title and Description are new
  (`TaskDetailPanel.tsx`, kept as private, non-exported subcomponents since they're single-purpose to this
  panel): Title mirrors `EditableTitleCell`'s click-to-edit pattern at heading size, and Description is the
  first place a task's description is actually editable (`taskFieldChange.description` — a genuinely new
  field, everything else already existed).
- **Attachments are not in this panel yet.** Listed in the same spec section but explicitly Phase 13, with
  no backing API — an empty placeholder section with nothing behind it would be a half-finished
  implementation, so it's left out until that phase adds real data. Comments and Activity History (also
  listed there) are now implemented — see below.

### Comments & activity history

Both `TaskComments` and `TaskActivities` tables were built in Phase 2, so — same story as Phase 10's
custom fields — this phase is API + service layer + UI on top of an already-designed schema.

- **Comments authorization is stricter than everywhere else in the app:** viewing/adding a comment uses
  the usual participate tier, but editing or deleting one is **author-only**, with no manage-tier or
  Administrator override — a literal reading of the spec ("edit their own comment," "delete their own
  comment"). Verified directly: even the Administrator account gets a 403 trying to edit another user's
  comment.
- **Activity history is built by diffing old vs. new values inside `TaskService`**, not by a generic
  before/after snapshot mechanism — every place a task's fields can change (`CreateAsync`, `UpdateAsync`,
  `DuplicateAsync`, `SetCustomValueAsync`) now also builds `TaskActivity` rows for whatever actually
  changed. `SortOrder` is deliberately excluded from diffing — reordering isn't a meaningful event for a
  human reading the feed, unlike every other field.
- **Assignment gets its own `"Assigned"` action** distinct from the generic `"FieldChanged"`, so it renders
  as "Shimon assigned this task to David" rather than "Shimon changed Assigned To from (none) to David" —
  matching the spec's example phrasing.
- **Backend stores raw values, frontend formats them for display** (`ActivityHistorySection.tsx`) — reuses
  the existing `STATUS_LABELS` map and `formatDate` utility rather than duplicating a labels dictionary on
  the backend. Priority values need no mapping since the enum strings ("Low"/"High"/...) are already
  human-readable.
- Custom field changes are logged too, using the field's own name (e.g. "changed Cost from (none) to
  1200.00") — raw stored values, not resolved option/user labels, since fully resolving every custom field
  type's display value for historical entries would be significant extra scope for a "nice to have" over
  what the spec actually asks for.
- Comment mutations use plain invalidate-on-success (no optimistic update) — unlike spreadsheet cell edits,
  a brief pending state on posting a comment is normal, expected UX, so the added complexity of an
  optimistic rollback path wasn't justified here.

### Attachments

The spec is explicit here: "design support for task attachments... do not implement complicated object
storage yet." `TaskAttachments` was built in Phase 2; this phase adds a real (but simple) storage
mechanism plus the API/UI on top of it — not a stub, but not S3/Azure Blob either.

- **Storage is local disk** (`AttachmentService`), under a configurable root (`Attachments:StorageRoot`,
  default `App_Data/attachments`, resolved relative to the content root) that sits **outside `wwwroot`** —
  files are never directly web-accessible by URL; every read goes through `GET /api/attachments/{id}/download`,
  which runs the same project-participation authorization check as everything else. Swapping this service
  for a cloud-storage-backed implementation later is a contained change (one class behind `IAttachmentService`),
  which is the point of scoping it this way now.
- **Filenames on disk are never client-controlled.** Each file is stored as `{taskId}/{newGuid}{extension}`;
  the original filename is kept only in the database, for display and for the `Content-Disposition` header
  on download. This avoids path traversal and collisions without needing to sanitize an arbitrary
  client-supplied filename into something safe to use as a real path.
- **Validation:** a 25 MB size cap (generous enough for phone photos and scanned PDFs — this app's actual
  domain of permits/inspections — raised from an initial 10 MB after real use showed that was too tight)
  and a small blocklist of dangerous executable extensions (`.exe`, `.dll`,
  `.bat`, `.cmd`, `.sh`, `.ps1`, `.msi`, `.com`, `.scr`) — enough to stop the obvious "someone uploads
  malware and a teammate downloads and runs it" risk without building a full content-scanning pipeline,
  which the spec doesn't ask for.
- **Delete authorization sits between comments' strict "author only" and tasks' "manage tier":** the
  uploader can always remove their own attachment (self-correction, same idea as comments), and
  Administrators/owners/manager-members can remove any attachment on the project (consistent with them
  managing everything else) — but a random Member can't delete a teammate's upload. The spec doesn't specify
  this explicitly, so this was a judgment call between the two authorization patterns already established
  elsewhere in the app, verified live: a Member with neither role gets a 403.
- Upload/delete are logged into the Phase 12 activity feed too (`"AttachmentAdded"`/`"AttachmentRemoved"`),
  reusing that infrastructure directly rather than building a separate history mechanism.
- **Frontend upload needs `FormData`, which `httpClient`'s JSON-only wrapper didn't support** — rather than
  add a parallel request function, `request()` now detects a `FormData` body and skips setting
  `Content-Type` itself, letting the browser set its own multipart boundary. Download is a plain `<a href>`
  to the download endpoint rather than a `fetch`+blob dance — cookie auth means the browser handles it like
  any other authenticated same-origin navigation.

## Requirements

- [.NET SDK 10.0+](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and npm
- [PostgreSQL 16+](https://www.postgresql.org/download/)
- Visual Studio 2022 (17.14+) or the `dotnet`/`npm` CLIs directly

## Installation

```bash
git clone <repository-url>
cd Silver-Task

# Backend
cd Silver-Task.Server
dotnet restore

# Frontend
cd ../silver-task.client
npm install
```

## Running the app

**Option A — Visual Studio:** open `Silver-Task.slnx` and press F5 with `Silver-Task.Server` as the
startup project. The SPA proxy launches the Vite dev server automatically.

**Option B — CLI:**

```bash
# Terminal 1 — API (auto-launches the Vite dev server via SpaProxy)
cd Silver-Task.Server
dotnet run
```

The app is served at `https://localhost:7001` (API) / `https://localhost:42665` (SPA dev server, proxied).

To run the frontend standalone against an already-running API:

```bash
cd silver-task.client
npm run dev
```

## Database setup & migrations

1. Install PostgreSQL 16+ and create a local database (any name works; the example below uses `silvertask_dev`).
2. Configure the connection string for local development using .NET User Secrets (never commit real
   credentials to `appsettings*.json`):

   ```bash
   cd Silver-Task.Server
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=silvertask_dev;Username=postgres;Password=postgres"
   ```

   In production, set the equivalent environment variable instead: `ConnectionStrings__DefaultConnection`.
3. EF Core tooling (`dotnet-ef`) is pinned as a local tool in `.config/dotnet-tools.json`. Restore it once
   per clone:

   ```bash
   cd Silver-Task   # repo root
   dotnet tool restore
   ```
4. Apply migrations to create/update the database schema:

   ```bash
   cd Silver-Task   # repo root
   dotnet ef database update --project Silver-Task.Server --startup-project Silver-Task.Server
   ```

To add a new migration after changing an entity or configuration:

```bash
dotnet ef migrations add <MigrationName> --project Silver-Task.Server --startup-project Silver-Task.Server --output-dir Data/Migrations
```

## Running tests

Not yet applicable — no automated test project exists in either the server or client as of v1.0.1.
This remains the single biggest open gap noted throughout Phases 45–49 (see `RELEASE_NOTES.md`'s
"Known limitations") and is expected to be its own dedicated phase rather than something folded into
a feature or stabilization phase.

## Environment variables

| Variable | Purpose | Where to set it |
|---|---|---|
| `ConnectionStrings:DefaultConnection` / `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | User Secrets (dev) / environment variable (prod) |
| `Jwt:Secret` | JWT signing key (32+ random bytes, e.g. `openssl rand -base64 48`) | User Secrets (dev) / environment variable (prod) |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | JWT validation/expiry configuration (non-secret defaults already in `appsettings.json`) | `appsettings.json`, override via env if needed |
| `Smtp:Host`, `Smtp:Port`, `Smtp:EnableSsl`, `Smtp:Username`, `Smtp:Password`, `Smtp:FromAddress`, `Smtp:FromName` | Outgoing email (notifications) — `Smtp:Host` unset means email is simply off everywhere; nothing else in the app depends on it | User Secrets (dev) / environment variable (prod), never `appsettings.json` |

ASP.NET Core does not read `.env` files directly. `.env.example` at the repo root documents the variable
names/shapes for reference; real values go through **User Secrets** locally (`dotnet user-secrets set`,
stored outside the repo) or **environment variables** in CI/production (using `__` as the config-section
separator). Never commit `appsettings.Development.json` with real credentials filled in — it currently has
none.

## Email notifications (Phase 45)

Extends the email-capable notification system already built in Phase 36 (`IEmailService`/
`EmailService`, `NotificationTemplates`, per-(user, notification-type) `InAppEnabled`/
`EmailEnabled` toggles, digest emails, quiet hours — all still exactly as they were) rather than
replacing it. What Phase 45 adds:

- **Background delivery queue** — `NotificationService.MaybeSendEmailAsync` no longer calls
  `IEmailService.SendAsync` inline on the caller's request thread; it enqueues a self-contained
  `EmailDelivery` row (`Status = Queued`) and returns immediately, so a slow/unreachable SMTP
  server can never block a task/comment/project write. `EmailDeliveryBackgroundService` (same
  `PeriodicTimer` + per-tick DI-scope pattern as every other background service in this app, e.g.
  `DueDateNotificationBackgroundService`) polls every 20 seconds via `IEmailDeliveryService`,
  re-validates the recipient's access, the task/project's continued existence, and (for
  `TaskDueSoon`/`TaskOverdue`) the task's current status immediately before sending — a
  notification queued while access was valid is never delivered after that access, or the
  underlying task/project, is gone. Retries twice with backoff (2 min, then 10 min) before marking
  a row `Failed`; `EmailDelivery.LastError` only ever stores a short, generic classification
  (e.g. `"SMTP error (GeneralFailure)."`), never raw exception text/credentials. Several
  deliveries for the same (recipient, notification type) claimed in the same tick — e.g. a bulk
  assignment — are coalesced into one "N updates" email instead of N near-identical ones.
- **User-level master email switch** — `UserPreference.EmailNotificationsEnabled` (default `true`),
  checked before any per-type `UserNotificationSetting.EmailEnabled` toggle. Settings → Notifications
  exposes it as a single top-level toggle above the existing per-type grid.
- **Admin email configuration** — Admin → System Settings now surfaces the `Notifications` section
  that already existed server-side (`EmailNotificationsEnabled`/`DailyDigestEnabled`/`RetentionDays`/
  `MaxBatchSize`) but had no UI, plus a new `General.ApplicationBaseUrl` setting email links are
  built against (falls back to the first configured `Cors:AllowedOrigins` entry if unset, for
  backward compatibility). Admin → Email adds SMTP status (configured yes/no, never the
  host/credentials themselves), a "Send Test Email" action, and a read-only delivery log
  (`Status`/`NotificationType`/`RecipientUserId`/timestamps/`LastError` only — no email body, no raw
  address).
- **Customizable templates** — `EmailTemplate` (one optional override row per notification type)
  lets an Administrator override the Subject/Heading/Body/CTA/Footer text for the five notification
  types with reusable copy (`TaskAssigned`, `MentionedInComment`, `TaskDueSoon`, `TaskOverdue`,
  `UserAddedToProject` — see `Common/DefaultEmailTemplates.cs`); every other notification type keeps
  using the original generic `NotificationTemplates.ForNotification` rendering unchanged. Template
  text may only reference a fixed variable allow-list (`{{UserName}}`, `{{ActorName}}`,
  `{{TaskName}}`, `{{ProjectName}}`, `{{DueDate}}`, `{{ActionUrl}}` — see
  `Common/EmailTemplateVariables.cs`) via simple, non-executing `{{Token}}` substitution
  (`EmailTemplateService.Substitute`); an unrecognized token is rejected at save time. The final
  composed string is HTML-encoded as a whole before rendering, so neither an admin's template text
  nor a substituted task/user/project name can inject markup. "Preview" renders with sample data and
  never sends; "Reset to Default" deletes the override row.
- **Security notes** — SMTP credentials only ever live in `Smtp:*` configuration (User
  Secrets/environment variables), never in the database, an API response, or a template; the
  delivery log and test-email/template endpoints are all `[Authorize(Roles = Administrator)]`; a
  user's own notification preferences are only ever readable/writable for that user
  (`User.GetUserId()`-scoped, same pattern as every other self-service settings endpoint).
- **Known limitation** — no new automated test project was added for this phase. `CLAUDE.md`
  explicitly lists automated tests as a separate, not-yet-started phase; standing up an xUnit/Vitest
  harness felt like a larger, separate decision than this phase's own scope. Verified instead via
  `dotnet build`/`npm run build`/`npm run typecheck`/`npm run lint`, a reviewed EF Core migration,
  and a live manual run-through (test email, template preview, queue → retry → cancel-on-deleted-task,
  master-toggle suppression, in-app/email independence) against the seeded dev database.

## Notification digests (Phase 46)

Replaces the Phase 36 global "Daily digest" mechanism (a single per-user `DigestFrequency`
switch, sent inline with no retry) with per-notification-type delivery modes and real Daily/Weekly
digest scheduling, reusing Phase 44's `Notification` records and Phase 45's `EmailDelivery`
queue/retry pipeline rather than building either again.

- **Delivery modes** — `UserNotificationSetting.EmailDeliveryMode` (`Immediately` / `DailyDigest` /
  `WeeklyDigest` / `Off`) replaces the old per-type `EmailEnabled` bool, one value per notification
  type (same EAV-style row Phase 36 already used for `InAppEnabled`, which is untouched — email
  delivery mode never affects whether something shows up in the Notification Center).
  Urgent-priority types (currently only `TaskOverdue`) always send immediately regardless of the
  stored mode — enforced both when the setting is saved (`UserNotificationSettingsService.UpdateAsync`)
  and when a notification is raised (`NotificationService.NotifyAsync`), and shown as "Always
  immediate" (dropdown disabled) in Settings → Notifications.
- **Schedule** — `UserPreference.DailyDigestTime`/`WeeklyDigestDay`/`WeeklyDigestTime` (defaults
  from admin-configurable `Notifications.DefaultDailyDigestTime`/`DefaultWeeklyDigestDay`/
  `DefaultWeeklyDigestTime` system settings, same pattern as `DefaultTimeZone`), interpreted in the
  user's own `TimeZone`. `DigestSchedulerBackgroundService` ticks every 10 minutes; a user is due
  when their local time has passed the configured time and `LastDailyDigestAt`/`LastWeeklyDigestAt`
  (advanced atomically with digest generation) isn't already today/this ISO week — deliberately no
  upper time-window bound, so a missed run (app was offline) still catches up the same day once the
  app resumes, without a separate "missed schedule" mechanism.
- **Content** — `DigestGenerationService` builds sections entirely from existing data: notification
  records for "what happened" (Assignments/Mentions/Comments/Status Changes/Priority Changes/Due
  Date Changes/Completed/Project Changes, each capped at 10 items with a "+N more" link, repeated
  same-task notifications collapsed into "N updates to X"), and live `Tasks`/`Projects` queries for
  "what's on your plate" (Overdue/Due Today/Upcoming, plus Completed This Week on the weekly
  digest) — re-joined against current project membership/ownership so access lost since a
  notification was raised silently excludes that item, not just its project name. No digest is
  enqueued when there's nothing to say (`LastDigestAt` still advances either way, so an empty
  window is never rescanned).
- **Delivery** — digest HTML is rendered once, at generation time, into `EmailDelivery.RenderedSubject`/
  `RenderedHtmlBody` and enqueued through the *same* Phase 45 queue/retry (2 min → 10 min backoff,
  `Failed` after 3 attempts) — never a second retry loop. A retry re-sends the exact same rendered
  content rather than re-scanning the window.
- **Templates** — two new admin-customizable pseudo-types (`DailyDigest`/`WeeklyDigest`,
  `Common/DefaultDigestTemplates.cs`) in the *same* `EmailTemplate` table/admin editor Phase 45
  built, with their own variable allow-list (`{{UserName}}`, `{{DigestDate}}`,
  `{{AssignmentCount}}`, `{{MentionCount}}`, `{{CommentCount}}`, `{{DueTodayCount}}`,
  `{{OverdueCount}}`, `{{DigestContent}}`, `{{ActionUrl}}`). `{{DigestContent}}` is the one
  variable substituted *after* every other token is substituted and the composed body is
  HTML-encoded as a whole — the only place real section markup enters the email, so admin template
  text still can't inject markup.
- **Known limitations** — (1) a notification type with `InAppEnabled = false` never produces a
  `Notification` row, so it can never appear in a digest even if its email mode is Daily/Weekly
  (digest content is deliberately sourced only from existing records, not a parallel event log —
  the sensible default is both channels enabled, so this only affects an explicit unusual
  combination); (2) no distributed lock — like every other background service in this app, the
  scheduler assumes a single running instance; (3) no new automated test project, same rationale as
  Phase 45. Verified via `dotnet build`/`npm run build`/`npm run typecheck`/`npm run lint`, a
  reviewed EF Core migration (including a hand-written backfill of `EmailEnabled` →
  `EmailDeliveryMode`), and a live run-through against the seeded dev database (per-category
  suppression while in-app stays independent, digest generation/content/enqueue, retry after a
  simulated SMTP failure, and — across two full app restarts — no duplicate digest for the same
  day).

## V1.0.0 release readiness (Phase 47)

Silver-Task 1.0.0 is the first release candidate. This section records the pre-release audit —
what was checked, what was fixed, and what's intentionally deferred — rather than re-describing
features already covered above.

**Audit method**: three parallel read-only code audits (security/authorization, performance/
database, migration/data-integrity) plus a live pass against the running dev server and database
(smoke test, IDOR/auth probes, an actual `pg_dump`/restore, and a from-empty migration run) — not
a new automated test suite (still a separate, not-yet-started phase; see "Running tests" above).

**Fixed this phase** (the only two High-severity findings across all three audits):
- `SearchService.SearchProjectsAsync` issued one `ProjectAccessService.GetProjectRoleAsync` DB
  round-trip per matched project instead of a single batched query — fixed to match the pattern
  `SearchTasksAsync` already used a few lines above it in the same file.
- `AutomationService.QueryAutomationsAsync` took its project/global scope filter as a plain
  `Func<Automation,bool>`, forcing EF Core to materialize (with `Conditions`/`Actions` eagerly
  included) **every** non-deleted automation in the system before filtering in memory — on every
  request to open a single project's Automations tab. Fixed by changing the parameter to
  `Expression<Func<Automation,bool>>` so the scope filter translates into SQL; this also surfaced
  and fixed a second, previously-inert bug in the same method (`string.Contains(..,
  StringComparison.OrdinalIgnoreCase)` for the search box, which only "worked" because it used to
  run in memory — replaced with `EF.Functions.ILike`, the pattern already used everywhere else in
  this codebase for case-insensitive search).
- Added `GET /api/health/ready` (anonymous, checks `Database.CanConnectAsync()`) alongside the
  existing `GET /api/health` liveness check — a load balancer/orchestrator should probe `/ready`
  to decide whether to route traffic to an instance, and `/health` for a cheap liveness ping.

**Known limitations / deferred findings** (Medium/Low — not release blockers per the gate below,
listed here so they're tracked rather than silently dropped):
- `SavedReport`/`SavedView`/`ProjectTemplate`/`TaskTemplate`'s `CreatedByUserId` FK is `Cascade`,
  while `Project.OwnerId` is `Restrict` — inconsistent policy for artifacts other users can depend
  on via their own `*Share` tables. Currently inert: `UserService.DeleteAsync` never hard-deletes a
  user (soft delete via `IsActive`/`IsDeleted` only), so this cascade path can't fire today. Worth
  revisiting only if a hard-delete admin path is ever added.
- Several background/list queries load a full (but currently modest) result set into memory before
  aggregating or checking (`ReportingService`'s non-paginated report methods, `UserService.GetAllAsync`,
  `ProjectService.GetAllForUserAsync`, `AutomationOverdueCheckBackgroundService`'s unbounded sweep
  query, a per-candidate dedup check in `NotificationService.CreateDueSoonAndOverdueNotificationsAsync`).
  None are expected to cause problems at V1 launch scale (dozens of users, hundreds of tasks); each
  is a candidate for the same batching/pagination treatment already applied elsewhere once real
  usage data shows it's needed.
- `TaskActivities` has separate single-column indexes on `TaskId` and `CreatedAt` rather than a
  composite `(TaskId, CreatedAt)` — fine at expected activity-history volume, would help once task
  histories get long.
- Frontend bundle is ~910KB (234KB gzipped) in one chunk (Vite's own size warning) — route-level
  code-splitting is a real but separate improvement, not attempted here.
- No automated test suite (unchanged from every prior phase's own note) — authorization discipline
  in particular is currently held together by consistent hand-written patterns (verified extensively
  in this audit) rather than something CI would catch on regression.

**Security audit result**: no Critical, High, or Medium findings. Authorization was checked across
every controller/service pair (IDOR via substituted GUIDs, cross-project access, admin-route
gating, unauthenticated access) both by static review and live probes against the running app —
every probe returned the expected 401/403/404, never leaked data. One Low finding (a hardcoded
demo-seed password) is already correctly gated to `--seed` + `Environment.IsDevelopment()` and
cannot run in production.

**Migration audit result**: PASS. All 24 migrations apply cleanly to a brand-new empty database
(verified by actually running `dotnet ef database update` against a fresh scratch database, not
just generating a script) and the resulting schema matches the existing dev database's table count
exactly. Both `Up()`/`Down()` of the two hand-edited Phase 45/46 migrations were re-verified for
correctness.

### Backup & restore (tested, not just documented)

Database backup/restore was performed for real against the dev database as part of this phase,
using PostgreSQL's own `pg_dump`/`pg_restore` (adjust host/db/user for your environment):

```bash
# Backup (custom format — supports selective/parallel restore)
pg_dump -h <host> -U <user> -d <database> -F c -f silvertask_backup.dump

# Restore into a fresh database
createdb -h <host> -U <user> <restore_target_db>
pg_restore -h <host> -U <user> -d <restore_target_db> silvertask_backup.dump
```

This was run end-to-end during the Phase 47 audit: a real dump of the dev database, restored into
a separate scratch database, with row counts, `__EFMigrationsHistory` count, and foreign-key
constraint count all confirmed to match exactly between source and restored copies before the
scratch database was dropped.

**File storage backup**: attachments live under `Attachments:StorageRoot` (default
`Silver-Task.Server/App_Data/attachments`) as a plain directory of server-generated-GUID-named
files — back it up with any file-level copy/sync tool (it has no database dependency of its own;
the `Attachments` table is the source of truth for filenames/metadata, the directory is the source
of truth for bytes, and both must be backed up together to be consistent). Restore is the reverse:
restore the database first, then restore the directory to the same `StorageRoot` path.

**Recommended production cadence**: nightly full `pg_dump` + continuous WAL archiving if the
hosting Postgres supports point-in-time recovery; file storage backed up on the same schedule as
the database so the two stay consistent.

### Production deployment checklist

- [ ] `ConnectionStrings__DefaultConnection` set via environment variable (never `appsettings.json`)
- [ ] `Jwt__Secret` set via environment variable, 32+ random bytes (app throws at startup if unset)
- [ ] `Smtp__Host`/`Smtp__Port`/`Smtp__EnableSsl`/`Smtp__Username`/`Smtp__Password`/
      `Smtp__FromAddress`/`Smtp__FromName` set if email notifications are wanted (app runs fully
      functional without them — email is simply off)
- [ ] `Cors:AllowedOrigins` configured to the real production origin(s) — empty by default, which
      means CORS allows nothing cross-origin (safe default, but must be set for any deployment
      where the SPA and API aren't served from the exact same origin)
- [ ] Reverse proxy terminates TLS and forwards to the app; `app.UseHttpsRedirection()` is already
      active in `Program.cs`
- [ ] Load balancer/orchestrator health checks point at `GET /api/health` (liveness) and
      `GET /api/health/ready` (readiness — verifies DB connectivity)
- [ ] `dotnet ef database update` run against the production database before first traffic
      (idempotent — safe to run on every deploy)
- [ ] Admin → Email → Send Test Email used to confirm SMTP configuration once deployed, before
      relying on it
- [ ] Backup cadence configured per the "Backup & restore" section above
- [ ] Confirm no `.env`/real secrets are present in the deployed image/container beyond what's
      injected via environment variables at runtime

### Disaster recovery procedure

1. Provision a fresh PostgreSQL instance (or confirm the existing one is reachable).
2. Restore the most recent database backup: `pg_restore -h <host> -U <user> -d <database>
   <backup.dump>` (create the target database first if it doesn't exist).
3. Restore the most recent attachment storage backup to the configured `Attachments:StorageRoot`
   path, from the same backup generation as the database restore in step 2 (mismatched generations
   mean either orphaned files with no DB row, or DB rows pointing at missing files — both are
   survivable but should be reconciled, not silently left).
4. Point the application's `ConnectionStrings__DefaultConnection` at the restored database and
   start the app — `dotnet ef database update` is safe to run again here even if the restored
   database is already fully migrated (idempotent).
5. Verify via `GET /api/health/ready`, then a real login + smoke test of the golden path before
   resuming production traffic.

## GitHub setup

- Never commit `.env`, real connection strings, credentials, or secrets — `.gitignore` blocks `.env*`
  (except `.env.example`) and `appsettings.*.local.json`.
- Build output (`bin/`, `obj/`, `node_modules/`, `dist/`) and IDE files (`.vs/`, `*.user`, `*.suo`) are
  already ignored.
- Uploaded attachments (`Silver-Task.Server/App_Data/`) are user content, not source — also ignored.

## Development phases

This project is being built incrementally. Completed phases:

- [x] **Phase 1** — Project architecture: TypeScript conversion, API client/provider/routing skeleton,
      backend middleware/CORS/health endpoint, verified builds and startup.
- [x] **Phase 2** — PostgreSQL database model and EF Core migrations: all 10 core tables, relationships,
      indexes, and the `InitialCreate` migration (see [Database schema](#database-schema)).
- [x] **Phase 3** — Authentication & users: password hashing, cookie-based JWT auth, secure-by-default
      authorization, `/api/auth` (login/logout/me) and `/api/users` endpoints, first-user-admin bootstrap,
      and a minimal login page + route guard on the frontend (see [Authentication](#authentication)).
- [x] **Phase 4** — Projects & project members: `/api/projects` (create/rename/archive) and
      `/api/projects/{id}/members` (list/add-by-email/remove) with membership-aware authorization, a real
      project list + creation form in the sidebar, and a project page for renaming and managing members
      (see [Projects & authorization model](#projects--authorization-model)).
- [x] **Phase 5** — Tasks REST API: full task CRUD + duplicate, assignee/completion/sort-order business
      rules, and a shared `ProjectAccessService` so task authorization can't drift from project
      authorization (see [Tasks](#tasks)). Backend-only by design — Phase 6 owns the spreadsheet UI that
      will consume this API.
- [x] **Phase 6** — Spreadsheet UI: TanStack Table grid with sticky header, resizable columns, and
      horizontal scroll; status/priority badges; the Table/Kanban/Calendar/Timeline/Gantt view-tab
      architecture (Table only implemented); inline task creation and row duplicate/delete (see
      [Spreadsheet UI](#spreadsheet-ui)). No inline cell editing yet — that's Phase 7.
- [x] **Phase 7** — Inline editing: click-to-edit Title/Start Date/Due Date with Enter-commits/
      Escape-cancels/Tab-moves-on, and optimistic updates with error rollback per the spec's required
      update→send→save→revert-on-failure flow (see [Inline editing](#inline-editing)). Status/Priority/
      Assigned To stay read-only pending Phase 8's dropdown editors.
- [x] **Phase 8** — Dropdown columns: Status/Priority as badge-styled `<select>` editors that commit on
      selection, and Assigned To populated from project members with an Unassigned option (see
      [Dropdown columns](#dropdown-columns)). All spreadsheet columns are now editable in place.
- [x] **Phase 9** — Filtering, sorting & search: client-side search (title/description), AND-combined
      filters (Status/Priority/Assigned To/Due-before), and sort across all 7 spec-listed fields via both
      a toolbar Sort menu and clickable column headers driving the same state (see
      [Filtering, sorting & search](#filtering-sorting--search)).
- [x] **Phase 10** — Custom fields: full CRUD for project-defined fields of all 10 spec-listed types
      (Text/Number/Currency/Date/DateTime/Checkbox/Dropdown/MultiSelect/User/LongText) plus a Link type
      added afterward on request, all on the Phase-2 EAV schema (Link needed no migration), per-type value
      validation, dynamic grid columns with type-appropriate editors, and search extended to custom text
      fields (see [Custom fields](#custom-fields)).
- [x] **Phase 11** — Task detail panel: a right-side drawer (spreadsheet stays visible behind it, per spec)
      opened via a dedicated expand icon rather than overloading cell clicks, URL-driven via `?task=<id>`,
      reusing the grid's own dropdown/date/custom-field editors unmodified and adding the first editable
      Description field (see [Task detail panel](#task-detail-panel)). No comments/activity/attachments
      sections yet — those are Phases 12–13.
- [x] **Phase 12** — Comments & activity history: author-only comment edit/delete (no admin override, per
      spec), and an activity feed built by diffing old vs. new values on every task/custom-field mutation,
      with a special-cased "assigned to" phrasing matching the spec's examples (see
      [Comments & activity history](#comments--activity-history)). Attachments remain Phase 13.
- [x] **Phase 13** — Attachments: local-disk storage architecture (deliberately not "complicated object
      storage," per spec) behind `IAttachmentService`, GUID-based filenames on disk with authorized-only
      download, a 25 MB size cap + blocked-extension validation, and upload/delete logged into the Phase 12
      activity feed (see [Attachments](#attachments)).

Phases 14–44 continued building out the application (below); the prose-per-phase documentation style
above wasn't kept up after Phase 13, so this range is listed by commit title only rather than
re-narrated after the fact — see `git log` for the actual diffs if you need phase-level detail beyond
what's in the rest of this README (which does document the current architecture regardless of which
phase introduced it):

- [x] **Phase 14** — Application/admin scaffolding expansion (large structural commit; see git history).
- [x] **Phase 15** — Incremental feature work (see git history).
- [x] **Phase 16** — Task management refinements (see git history).
- [x] **Phase 17–20** — Project page feature build-out (see git history).
- [x] **Phase 21** — Incremental refinements (see git history).
- [x] **Phase 22** — My Tasks / Project page polish (see git history).
- [x] **Phase 23** — Project settings & user preferences groundwork (see git history).
- [x] **Phase 24** — Admin System Settings — the generic key/value system-settings store (`SystemSettings`
      table, `SystemSettingDefinitions`) still used by every later admin-configurable feature.
- [x] **Phase 25** — Admin Custom Fields — admin-side management of project custom field definitions.
- [x] **Phase 26** — User Management and Delete User.
- [x] **Phase 27** — Security, permissions, and final review — the project-membership-based
      `ProjectAccessService` tiers (`EnsureCanParticipate/Edit/ManageAsync`) this codebase still uses
      everywhere (see [Projects & authorization model](#projects--authorization-model)).
- [x] **Phase 28** — Notifications — the original `Notification`/`UserNotificationSettings` tables and
      in-app notification center, later substantially extended in Phases 36 and 44.
- [x] **Phase 29** — Task Dependencies (finish-to-start and related types).
- [x] **Phase 30** — Subtasks.
- [x] **Phase 31** — Recurring Tasks.
- [x] **Phase 32** — Advanced Permissions — per-project roles (Manager/Member/Viewer) on top of Phase 27's
      system-wide roles.
- [x] **Phase 33** — File and Attachment Management (generalized beyond Phase 13's task-only attachments).
- [x] **Phase 34** — File Organization (folders/categories/tags for files).
- [x] **Phase 35** — Advanced Task Automation — the trigger → condition → action automation pipeline.
- [x] **Phase 36** — Advanced Notifications — email-capable notifications, daily digest groundwork, quiet
      hours; the direct predecessor to Phases 45–46 below.
- [x] **Phase 37** — Advanced Dashboard and Personal Workspace.
- [x] **Phase 38** — Advanced Reporting and Analytics.
- [x] **Phase 39** — Advanced Task Dependencies and Workflow Automation.
- [x] **Phase 40** — Advanced Task and Project Templates.
- [x] **Phase 41** — Advanced Custom Fields and Dynamic Forms.
- [x] **Phase 42** — Advanced Search and Global Search.
- [x] **Phase 43** — Saved Views and Advanced Filters.
- [x] **Phase 44** — Notifications & Notification Center — the in-app Notification Center UI, notification
      preferences, and real-time push (SignalR) this README's later phases build on directly.
- [x] **Phase 45** — Email Notifications and Templates (see
      [Email notifications](#email-notifications-phase-45) below).
- [x] **Phase 46** — Scheduled Notifications and Digests (see
      [Notification digests](#notification-digests-phase-46) below).
- [x] **Phase 47** — Final V1 QA, Security Hardening & Release Preparation (see
      [V1.0.0 release readiness](#v100-release-readiness-phase-47) below).
- [x] **Phase 48** — Production Deployment Prep — built and ran the actual `dotnet publish -c Release`
      artifact for the first time, which surfaced and fixed two real production-only bugs invisible in dev
      mode (see `DEPLOYMENT.md`). Version 1.0.0 released.
- [x] **Phase 49** — Post-Release Stabilization — a renewed code-level review (fixed two High-severity
      information-disclosure/diagnosability issues, one Medium frontend gap) since v1.0.0 was never
      actually deployed to a real production environment. Version 1.0.1 released (see `RELEASE_NOTES.md`).

Upcoming: performance work (real-scale/virtualization), automated testing, and production hardening.
