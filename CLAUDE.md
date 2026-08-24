# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Silver-Task is a production-oriented, spreadsheet-style task management app. Projects contain tasks
rendered in an editable, sortable, filterable grid (rows = tasks, columns = fields, including
project-defined custom fields), backed by a real REST API and PostgreSQL — no mock data, no hardcoded
task data in the UI. It started from the standard Visual Studio "React and ASP.NET Core" template and has
since been built out through 13 completed phases (see `README.md` → "Development phases" for the full
phase-by-phase history and the reasoning behind each design decision).

Two projects wired into one solution (`Silver-Task.slnx`):
- **`Silver-Task.Server`** — ASP.NET Core (.NET 10) Web API. Entry point `Program.cs`.
- **`silver-task.client`** — React 19 + TypeScript SPA built with Vite. Entry point `src/main.tsx`.

The server has a `ProjectReference` to the client (`ReferenceOutputAssembly=false`) purely so building/
publishing the server also builds the SPA and copies it into `wwwroot` — the server is the single
deployable unit in production (`app.UseDefaultFiles()` / `app.MapStaticAssets()` /
`app.MapFallbackToFile("/index.html")` in `Program.cs`).

**Read `README.md` before making non-trivial changes.** It documents the "why" behind almost every
architectural decision in this codebase (authorization tiers, EAV custom fields, activity-diffing,
TanStack Table version choice, etc.) in far more depth than is repeated here.

## Working conventions for this repo

- **Work one phase/feature at a time and stop for explicit approval before continuing** — this project has
  been driven through a strict phase-gated plan; do not jump ahead to unrequested work (performance/
  virtualization, automated tests, and production hardening are the three phases still not started).
- **Do not change the core stack**: PostgreSQL (not SQLite), React (not another frontend framework),
  ASP.NET Core (not Node.js). All primary keys are `Guid`/`uuid`; enums are stored as `varchar` via
  `.HasConversion<string>()` specifically so new enum values (e.g. a new custom field type) never require
  a migration.
- **After changes that touch both projects, build both and fix all errors** before considering the work
  done (`dotnet build` and `npm run build`/`npm run typecheck`).
- Never use mock/fake API data in production UI code — the frontend talks to the real API layer
  (`src/api/`) exclusively.

## Commands

**Server** (`Silver-Task.Server/`):
```bash
dotnet run                 # starts the API; in Development, auto-launches the Vite dev server via SpaProxy
dotnet build
```

**Client** (`silver-task.client/`):
```bash
npm install
npm run dev                # Vite dev server only; expects the backend running separately for API proxying
npm run build               # tsc -b && vite build — production build consumed by the server via MapStaticAssets
npm run typecheck           # tsc -b --noEmit
npm run lint                # oxlint (see below — not ESLint)
npm run preview
```

**Database migrations** (from repo root; `dotnet-ef` is pinned via `.config/dotnet-tools.json` — run
`dotnet tool restore` once per clone):
```bash
dotnet ef database update --project Silver-Task.Server --startup-project Silver-Task.Server
dotnet ef migrations add <MigrationName> --project Silver-Task.Server --startup-project Silver-Task.Server --output-dir Data/Migrations
```

Connection string and JWT secret are configured via `dotnet user-secrets` in dev (never committed to
`appsettings*.json`) and environment variables (`ConnectionStrings__DefaultConnection`, `Jwt__Secret`, using
`__` as the section separator) in production. See README → "Environment variables" for the full table.

**Tests**: none yet — no test projects exist in either the server or client (this is Phase 15, not started).

## Dev workflow: SPA proxy

In development the two projects run as separate processes proxied together — a single `dotnet run` does
**not** serve the built SPA in dev mode:
- `Silver-Task.Server.csproj` sets `SpaProxyLaunchCommand=npm run dev` / `SpaProxyServerUrl=https://localhost:42665`
  and references `Microsoft.AspNetCore.SpaProxy`; starting the server in Development auto-launches Vite and
  proxies non-API requests to it.
- `silver-task.client/vite.config.js` proxies `/api/*` to the ASP.NET backend (resolved from
  `ASPNETCORE_HTTPS_PORT`/`ASPNETCORE_URLS`, falling back to `https://localhost:7001`). **Adding a new
  controller doesn't require touching this** — the proxy rule is already a catch-all on `/api`.
- Launch profiles (`Properties/launchSettings.json`): `http` → `http://localhost:5056`; `https` →
  `https://localhost:7001;http://localhost:5056`.
- Before starting either dev server yourself, check whether the user already has one running (e.g. from
  Visual Studio) — don't kill processes you didn't start.

## Linting

Client linting uses **oxlint**, not ESLint (`silver-task.client/.oxlintrc.json`, `react`/`oxc` plugins).
`react/rules-of-hooks` is an error. `react/only-export-components` is a warning that bites whenever a
shared constant (e.g. a labels map) is exported alongside a component from the same file — it breaks Fast
Refresh eligibility. Fix by moving shared constants into a non-component file (a hook or types file), not
by suppressing the rule.

## Backend architecture

Layered: **Controllers → Services → EF Core**. Controllers (`Controllers/`) are thin — they extract the
caller's identity via `User.GetUserId()`/`User.GetRole()` (claims extensions in `Common/`) and delegate
everything else to a service; no business logic or DbContext access in a controller. Services (`Services/`,
one interface + implementation per file) hold all business logic and are the only layer that touches
`AppDbContext` (`Data/AppDbContext.cs`). DTOs (`Models/DTOs/`) are the only shapes ever returned by the API
— entities never round-trip to the client directly.

**Shared authorization primitive — `ProjectAccessService`**: almost every other service depends on this
one to avoid authorization rules drifting between resources. It exposes two tiers, both keyed off runtime
project membership rather than just role:
- `EnsureCanParticipateAsync` — Administrator, the project owner, or any project member. Used for viewing
  and most task/custom-field-value edits.
- `EnsureCanManageAsync` — Administrator, the project owner, or a `Manager` who is also a member of that
  specific project. Used for project management, task deletion, and custom field *definition* changes.

Two services intentionally use a **third, stricter tier** instead: comments are author-only (edit/delete)
with *no* manage-tier or Administrator override, and attachment deletion splits the difference (uploader
can always delete their own; otherwise the manage tier applies). See README → "Comments & activity
history" / "Attachments" for the reasoning.

**Other structural notes:**
- The task entity/table is `TaskItem`/`Tasks`, not `Task` — avoids colliding with
  `System.Threading.Tasks.Task`, which is implicitly in scope via `ImplicitUsings`.
- Custom fields (`CustomFields`/`CustomFieldOptions`/`TaskCustomValues`) use an EAV pattern so new field
  types (10 built in Phase 10, plus a `Link` type added afterward) never require a schema migration —
  every value is stored as text and validated/normalized per `FieldType` in
  `TaskService.ValidateAndNormalizeCustomValueAsync`. `FieldType` itself is immutable after creation.
- Activity history (`TaskActivities`) is built by **diffing old vs. new values inline** inside
  `TaskService` (`CreateAsync`/`UpdateAsync`/`DuplicateAsync`/`SetCustomValueAsync`), not a generic
  snapshot/audit mechanism. `SortOrder` changes are deliberately excluded from the diff.
- `SortOrder` on tasks is a fractional `double` index (new tasks append at `max + 1`; duplicates insert at
  the midpoint after the original) so drag-reorder never needs to renumber siblings.
- Auth is cookie-based JWT: on login the API sets an httpOnly/Secure/SameSite=Strict cookie
  (`silvertask_auth`) rather than returning the token in the response body. `Program.cs` sets a global
  `FallbackPolicy` requiring authentication on any endpoint without explicit `[Authorize]`/
  `[AllowAnonymous]` — new controllers are unauthenticated-by-default-denied, not opt-in secured.
  `AddJsonOptions` registers `JsonStringEnumConverter()`; without it, enum fields like `"role":"Administrator"`
  fail to deserialize from JSON (System.Text.Json defaults to numeric enums).
- Domain exceptions (`Common/Exceptions/{NotFoundException,ConflictException,ForbiddenException,ValidationException}`)
  are mapped to HTTP status codes centrally by `Middleware/ExceptionHandlingMiddleware.cs` (404/409/403/400).
  Unexpected exceptions are logged in full server-side but return only a generic message + traceId.
- Attachments are stored on local disk (`AttachmentService`, root configurable via
  `Attachments:StorageRoot`, default `App_Data/attachments`) outside `wwwroot` — never directly
  web-accessible; every read goes through the authorized `GET /api/attachments/{id}/download` endpoint.
  Filenames on disk are always server-generated GUIDs; the client-supplied original name is kept only in
  the database. This is deliberately "not complicated object storage" per the spec — swapping the storage
  backend later is a contained change behind `IAttachmentService`.

## Frontend architecture

- **API layer** (`src/api/`): a centralized fetch wrapper (`httpClient.ts`) is the only thing that talks to
  the backend. It sends `credentials: 'include'` for the auth cookie and detects `FormData` bodies
  (attachment uploads) to skip setting `Content-Type` so the browser can set its own multipart boundary.
- **Server state is TanStack Query, exclusively** — no separate client-side data store. The recurring
  pattern, established in `useUpdateTask` (`hooks/useTasks.ts`) and reused for
  `useSetTaskCustomValue`/dropdown edits, is optimistic-update-with-rollback: `onMutate` patches the query
  cache immediately (before the network call resolves), snapshots prior state, and `onError` restores that
  snapshot; `onSuccess` reconciles with the server's actual response rather than refetching. A failed edit
  leaves the cell with a red outline (`editable-cell--error`) until the next edit attempt.
- Since `PUT /api/tasks/{id}` is a full-resource replace, a single-field edit still has to send every other
  current value — `buildBaseRequest` fills in the unchanged fields, and each editable field has its own
  `{ optimistic, request }` pair in the `taskFieldChange` map (`hooks/useTasks.ts`), because some fields'
  optimistic-cache shape and API-request shape differ (e.g. assignee: cache needs a `UserSummary` object,
  the request needs a bare `assignedToUserId`).
- **`TaskTable` (`components/spreadsheet/TaskTable.tsx`) deliberately uses `useLegacyTable`** from
  `@tanstack/react-table/legacy` — the installed v9 replaced `useReactTable` with a new modular
  `features`-based `useTable` API, but the officially-bundled v8-compatible legacy layer still covers
  everything this grid needs (including column resizing) and is simpler/lower-risk for a plain display
  grid. This is a considered choice, not leftover migration debt — revisit only if a future feature needs
  something v9-only.
- Filtering/sorting/search (`hooks/useTaskFilters.ts`) is **fully client-side** `useMemo` over the
  already-loaded task list — there's no pagination yet, so there's nothing to gain from round-tripping to
  the server per keystroke. This is explicitly deferred to the not-yet-started performance phase once real
  scale requires it.
- The task detail panel is opened via a dedicated expand icon (not by clicking a task row/cell, which
  already does inline editing) and its open/closed state lives in the `?task=<id>` URL search param, not
  local component state — makes it linkable and back-button-closeable. It reuses the grid's own dropdown/
  date/custom-field cell components unmodified inside a form layout.
- Custom field cell editors (`components/spreadsheet/*CustomValueCell.tsx`) reuse the interaction patterns
  already established for built-in columns rather than inventing new ones per type (click-to-edit text,
  always-rendered `<select>` for Dropdown/User, a `<details>` checklist popover for MultiSelect, a two-field
  popover for Link). All go through `useSetTaskCustomValue`.
- The `@` import alias (`vite.config.js`) resolves to `silver-task.client/src`.

## Database schema

EF Core (Npgsql), configured via `IEntityTypeConfiguration<T>` classes in `Data/Configurations/`. Ten
tables: `Users`, `Projects`, `ProjectMembers`, `Tasks`, `TaskComments`, `TaskActivities`,
`TaskAttachments`, `CustomFields`, `CustomFieldOptions`, `TaskCustomValues`. See README → "Database schema"
for the full per-table purpose table and the reasoning behind `Guid` PKs / string-backed enums.
