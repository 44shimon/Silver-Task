# Silver-Task

A production-oriented, spreadsheet-style task management application. Projects contain tasks displayed
in an editable, sortable, filterable grid (rows = tasks, columns = fields, including project-defined
custom fields), backed by a real REST API and a relational database.

> **Status:** Phase 1 (project architecture) and Phase 2 (database schema & migrations) complete.
> Authentication and the spreadsheet UI are not implemented yet — see [Development phases](#development-phases).

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
│  ├─ Models/Entities/        EF Core entities + enums (Status, Priority, Role, CustomFieldType)
│  ├─ Data/                   AppDbContext, Fluent API configurations, EF Core migrations
│  ├─ Middleware/             Cross-cutting concerns (exception handling, etc.)
│  ├─ Program.cs              App startup, DI, middleware pipeline
│  └─ appsettings*.json       Environment configuration
├─ silver-task.client/        React + TypeScript SPA
│  └─ src/
│     ├─ api/                 Centralized API client (fetch wrapper + per-resource services)
│     ├─ components/layout/   App shell (topbar, sidebar)
│     ├─ hooks/                React Query hooks
│     ├─ pages/                Route-level views
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
| `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience` (Phase 3) | JWT signing configuration | User Secrets (dev) / environment variable (prod) |

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

Upcoming: authentication, projects & members, tasks REST API, spreadsheet UI, inline editing, dropdown
columns, filtering/sorting/search, custom fields, task detail panel, comments & activity history,
attachments, performance work, testing, and production hardening.
