# Silver-Task

A production-oriented, spreadsheet-style task management application. Projects contain tasks displayed
in an editable, sortable, filterable grid (rows = tasks, columns = fields, including project-defined
custom fields), backed by a real REST API and a relational database.

> **Status:** Phases 1–12 complete (architecture, database schema, authentication & users, projects &
> members, tasks REST API, spreadsheet UI, inline editing, dropdown columns, filtering/sorting/search,
> custom fields, task detail panel, comments & activity history). Attachments are not implemented yet —
> see [Development phases](#development-phases).

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
│     ├─ components/spreadsheet/ TaskTable (TanStack Table) + cell editors + TaskDetailPanel + comments/activity
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

Not yet applicable — test projects are introduced in Phase 15.

## Environment variables

| Variable | Purpose | Where to set it |
|---|---|---|
| `ConnectionStrings:DefaultConnection` / `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | User Secrets (dev) / environment variable (prod) |
| `Jwt:Secret` | JWT signing key (32+ random bytes, e.g. `openssl rand -base64 48`) | User Secrets (dev) / environment variable (prod) |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | JWT validation/expiry configuration (non-secret defaults already in `appsettings.json`) | `appsettings.json`, override via env if needed |

ASP.NET Core does not read `.env` files directly. `.env.example` at the repo root documents the variable
names/shapes for reference; real values go through **User Secrets** locally (`dotnet user-secrets set`,
stored outside the repo) or **environment variables** in CI/production (using `__` as the config-section
separator). Never commit `appsettings.Development.json` with real credentials filled in — it currently has
none.

## GitHub setup

- Never commit `.env`, real connection strings, credentials, or secrets — `.gitignore` blocks `.env*`
  (except `.env.example`) and `appsettings.*.local.json`.
- Build output (`bin/`, `obj/`, `node_modules/`, `dist/`) and IDE files (`.vs/`, `*.user`, `*.suo`) are
  already ignored.

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

Upcoming: attachments, performance work, testing, and production hardening.
