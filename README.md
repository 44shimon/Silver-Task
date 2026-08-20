# Silver-Task

A production-oriented, spreadsheet-style task management application. Projects contain tasks displayed
in an editable, sortable, filterable grid (rows = tasks, columns = fields, including project-defined
custom fields), backed by a real REST API and a relational database.

> **Status:** Phases 1–6 complete (architecture, database schema, authentication & users, projects &
> members, tasks REST API, spreadsheet UI). Inline cell editing, dropdown editors, and filter/sort/search
> are not implemented yet — see [Development phases](#development-phases).

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
│  ├─ Services/                Business logic (auth, users, projects, tasks, JWT issuance), one interface + impl per file
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
│     ├─ components/spreadsheet/ TaskTable (TanStack Table) + badges + row/creation controls
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
- Row actions (duplicate/delete) call the Phase 5 API directly; there's no inline cell editing yet — that's
  Phase 7. Clicking a task's title/status/etc. does nothing yet.
- Project member management (Phase 4) was moved into a collapsed `<details>` section below the grid so the
  spreadsheet is the visually dominant element, per the app's intended layout.

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

Upcoming: inline editing, dropdown columns, filtering/sorting/search, custom fields, task detail panel,
comments & activity history, attachments, performance work, testing, and production hardening.
