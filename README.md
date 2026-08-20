# Silver-Task

A production-oriented, spreadsheet-style task management application. Projects contain tasks displayed
in an editable, sortable, filterable grid (rows = tasks, columns = fields, including project-defined
custom fields), backed by a real REST API and a relational database.

> **Status:** Phase 1 (project architecture) complete. Authentication, the database schema, and the
> spreadsheet UI are not implemented yet — see [Development phases](#development-phases).

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
│  ├─ Models/                 Entities, DTOs, shared response shapes
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

## Requirements

- [.NET SDK 10.0+](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and npm
- [PostgreSQL 16+](https://www.postgresql.org/download/) (required starting Phase 2)
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

Not yet applicable — the EF Core model and PostgreSQL wiring are introduced in Phase 2. This section
will be filled in with `dotnet ef migrations add` / `dotnet ef database update` instructions and
connection string configuration once that phase lands.

## Running tests

Not yet applicable — test projects are introduced in Phase 15.

## Environment variables

No secrets are required yet. Once authentication and the database are introduced, this project will use:

- A PostgreSQL connection string (via ASP.NET Core configuration / user secrets, never committed)
- A JWT signing secret (via configuration / user secrets, never committed)
- `VITE_API_URL` or equivalent for the frontend, if the API is ever hosted on a different origin

`.env` files are already excluded via `.gitignore`; a `.env.example` will be added when real
environment variables are introduced.

## GitHub setup

- Never commit `.env`, real connection strings, credentials, or secrets — `.gitignore` blocks `.env*`
  (except `.env.example`) and `appsettings.*.local.json`.
- Build output (`bin/`, `obj/`, `node_modules/`, `dist/`) and IDE files (`.vs/`, `*.user`, `*.suo`) are
  already ignored.

## Development phases

This project is being built incrementally. Completed phases:

- [x] **Phase 1** — Project architecture: TypeScript conversion, API client/provider/routing skeleton,
      backend middleware/CORS/health endpoint, verified builds and startup.

Upcoming: PostgreSQL/EF Core schema, authentication, projects & members, tasks REST API, spreadsheet UI,
inline editing, dropdown columns, filtering/sorting/search, custom fields, task detail panel, comments &
activity history, attachments, performance work, testing, and production hardening.
