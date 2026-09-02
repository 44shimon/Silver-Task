# Silver Task

A production-oriented, spreadsheet-style task management application. Projects contain tasks
displayed in an editable, sortable, filterable grid (rows = tasks, columns = fields, including
project-defined custom fields), backed by a real REST API and a PostgreSQL database — no mock
data, no hardcoded task data anywhere in the UI.

> **Status:** v1.0.1 released (Phases 1–49 complete). See
> [Release Information](#release-information) for the full phase-by-phase history and
> `RELEASE_NOTES.md` for what changed in each release. Performance work at real scale and an
> automated test suite remain the two biggest open gaps — see [Known limitations](#known-limitations).

## Table of contents

[Features](#features) · [Architecture](#architecture) · [Requirements](#requirements) ·
[Quick Start](#quick-start) · [Production Installation](#production-installation-debian) ·
[Development Installation](#development-installation) · [Configuration](#configuration) ·
[Database](#database) · [File Storage](#file-storage) · [Authentication](#authentication) ·
[Email Configuration](#email-configuration) · [Background Workers](#background-workers) ·
[Updating](#updating) · [Upgrade Engine](#upgrade-engine) · [Backup](#backup) · [Restore](#restore) ·
[Troubleshooting](#troubleshooting) · [Security](#security) · [Uninstallation](#uninstallation) ·
[Project Structure](#project-structure) · [Development](#development) · [Testing](#testing) ·
[Release Information](#release-information) ·
[Appendix: feature & phase implementation notes](#appendix-feature--phase-implementation-notes)

## Features

Everything below is implemented and reachable in the running app — this list intentionally omits
anything not actually built:

- **Projects** with membership, per-project roles (Manager/Member/Viewer) on top of system-wide
  roles (Administrator/Manager/Member)
- **Tasks & Subtasks**, with **Task Dependencies** (finish-to-start and related types)
- Five task views over the same data: **Sheet/Table**, **Kanban**, **Calendar**, **Timeline**, **Gantt**
- **Global Search**, **Advanced Filters**, **Saved Views**
- **Custom Fields** (10+ types, EAV-based — a new type never needs a schema migration) and
  **Project/Task Templates**
- **Comments** with **@Mentions**, **File Attachments**, and a diffed **Activity History**
- **Automations** — trigger → condition → action pipelines with an execution history
- **Dashboards** and **Reports**
- **In-app Notifications** (Notification Center, real-time via SignalR) and **Email Notifications**
  reusing the same event pipeline, with **Daily/Weekly Digests** and admin-customizable email
  templates
- **User Settings** (profile, preferences, notification delivery modes) and **Admin/System
  Settings** (organization-wide defaults, all server-validated)
- **User Management**, **Roles & Permissions** enforced consistently through one shared
  authorization service (audited — see [Security](#security))

## Architecture

The frontend and backend are separate projects that communicate exclusively over a REST API — the
React app never talks to the database directly. In production, the ASP.NET Core app also serves
the compiled SPA as static files from the same origin, so there's a single deployable process
behind the reverse proxy:

```text
                     ┌───────────────┐
                     │    Browser    │
                     └───────┬───────┘
                             │ HTTPS
                             ▼
                     ┌───────────────┐
                     │     nginx     │  reverse proxy, TLS termination,
                     │ (reverse proxy)│  WebSocket upgrade for /hubs/notifications
                     └───────┬───────┘
                             │ HTTP (loopback only)
                             ▼
              ┌──────────────────────────────┐
              │   Silver-Task.Server (Kestrel) │  ASP.NET Core Web API
              │   + bundled React SPA (wwwroot)│  serves both API and frontend
              │   + 7 in-process background    │  email delivery, digests, due-date
              │     workers                    │  sweeps, automations, recurring tasks
              └───────────────┬────────────────┘
                               │
                 ┌─────────────┴─────────────┐
                 ▼                           ▼
          ┌────────────┐              ┌────────────┐
          │ PostgreSQL │              │File Storage│  local disk, outside wwwroot,
          │            │              │(attachments)│  never directly web-accessible
          └────────────┘              └────────────┘
```

Backend concerns are layered into Controllers (thin) → Services (all business logic + the only
layer touching the database) → EF Core; frontend concerns are layered into a centralized API
client, TanStack Query hooks, and presentational components. See
[Project Structure](#project-structure) for the actual directory layout and the
[Appendix](#appendix-feature--phase-implementation-notes) for the reasoning behind specific
architectural choices (why cookie-based JWT, why EAV custom fields, why background workers run
in-process, etc.).

**Frontend** (`silver-task.client/`) — React 19 + TypeScript, Vite, TanStack Table (grid), TanStack
Query (server state), React Router, Lucide React (icons).

**Backend** (`Silver-Task.Server/`) — ASP.NET Core (.NET 10) Web API, Entity Framework Core
(Npgsql), PostgreSQL, cookie-based JWT authentication, SignalR (real-time notifications).

**No Docker/containers** — this repository has no Docker or Compose configuration; the Debian
installer (below) provisions .NET, Node.js (build-time only), PostgreSQL, and nginx directly on
the host via `apt`, and runs the app as a systemd service.

## Requirements

**To run the Debian installer:**
- A fresh or existing **Debian 12 (bookworm) or newer** server (amd64 or arm64)
- Root or sudo access
- At least 2GB free disk space and 1GB RAM (more if you expect a large task/attachment volume)
- Outbound internet access (to install packages and, if a domain is configured, to reach Let's
  Encrypt)
- Ports 80 and/or 443 free on the host (or whichever ports you configure — see
  [Production Installation](#production-installation-debian))

**For development** (see [Development Installation](#development-installation)):
- [.NET SDK 10.0+](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and npm
- [PostgreSQL 16+](https://www.postgresql.org/download/)
- Visual Studio 2022 (17.14+) or the `dotnet`/`npm` CLIs directly

The installer checks OS, architecture, root access, disk space, RAM, port availability, and
internet connectivity before making any changes, and fails with a clear message rather than
partially installing if any check doesn't pass.

## Quick Start

```bash
git clone https://github.com/44shimon/Silver-Task.git
cd Silver-Task
sudo ./scripts/install-debian.sh
```

Answer the prompts (or pass `--non-interactive` — see below), including an administrator email —
the installer creates that account for you once the app is healthy, with a randomly generated
password printed once at the end (it automatically becomes Administrator; that's the application's
own bootstrap rule for the first account ever created, not something the installer has to
special-case). Leave the email prompt blank to skip and create the account yourself later, e.g.:

```bash
curl -X POST http://127.0.0.1:5000/api/users -H 'Content-Type: application/json' \
  -d '{"name":"Your Name","email":"you@example.com","password":"a-strong-password"}'
```

## Production Installation (Debian)

`scripts/install-debian.sh` installs Silver Task on the Debian server it's run on: .NET SDK,
Node.js (needed once, to build the SPA), PostgreSQL, nginx, and a systemd service — reusing exactly
the architecture in `deploy/` (`silvertask.service`, `nginx.conf`, `silvertask.env.example`), which
this script is what actually executes instead of a human following those files by hand.

**Run it from inside a cloned copy of this repository** (see [Quick Start](#quick-start)) — the
script installs the checkout it's run from; it does not re-clone from GitHub itself. This means
what gets installed is exactly what you have checked out (a specific tag, branch, or commit),
never a surprise.

### Interactive installation

```bash
sudo ./scripts/install-debian.sh
```

Prompts for: domain name (optional — leave blank for IP-only HTTP access), HTTP/HTTPS ports,
whether to request a Let's Encrypt certificate now, and optional SMTP settings (email works fully
without this — it's simply off until configured). It does **not** prompt for an admin
email/password — Silver Task has no installer-driven account creation; the first person to
register through the web UI becomes Administrator.

### Non-interactive installation

```bash
sudo ./scripts/install-debian.sh --non-interactive \
    --domain=tasks.example.com \
    --smtp-host=smtp.example.com --smtp-port=587 \
    --smtp-username=notifications@example.com --smtp-password="$SMTP_PASSWORD"
```

All flags are optional with safe defaults; see `sudo ./scripts/install-debian.sh --help` for the
full list (`--domain`, `--http-port`, `--https-port`, `--skip-ssl`, `--skip-firewall`,
`--smtp-*`), or set the equivalent `SILVERTASK_*` environment variables instead — useful for
config-management tools (Ansible, etc.) that prefer environment injection over CLI flags.

### What the installer does

1. Pre-flight checks (OS, architecture, root, disk/RAM, ports, internet).
2. If already installed, warns and either stops (interactive) or safely re-applies configuration
   without touching existing data (non-interactive) — see [idempotency note](#troubleshooting).
3. Installs system packages, .NET SDK, Node.js, enables PostgreSQL and nginx.
4. Creates a dedicated, unprivileged `silvertask` service account.
5. Copies the current checkout into `/opt/silver-task/source` (`.git` included, so
   [Updating](#updating) can `git pull` it later).
6. Creates the PostgreSQL database/role (generated password — see [Security](#security)), the file
   storage directory, and `/etc/silvertask/silvertask.env` (generated JWT secret, DB password;
   **existing values are always preserved**, never silently regenerated, if you re-run the
   installer against an already-configured system).
7. Builds (`dotnet publish -c Release`, which also builds the SPA via the project reference) and
   runs database migrations — **stops immediately, before starting anything, if either fails.**
8. Installs and enables the systemd service and nginx site config, requests a Let's Encrypt
   certificate if a domain was given, configures the firewall (only the ports actually needed —
   PostgreSQL is never exposed), starts everything.
9. Runs a real health check (`GET /api/health/ready`, which verifies database connectivity, not
   just "the process started") and refuses to declare success if it doesn't pass.
10. Prints the final URL and installation summary.

Every step is logged to `/var/log/silver-task-install.log` (secrets are never written to it, only
generated/reused — never their values). If a step fails, the script stops immediately with the
failed step, a diagnostic hint, and a non-zero exit code — it never continues past a
database/migration/build/health-check failure.

## Development Installation

```bash
git clone https://github.com/44shimon/Silver-Task.git
cd Silver-Task

# Backend
cd Silver-Task.Server
dotnet restore

# Frontend
cd ../silver-task.client
npm install
```

**Database**: install PostgreSQL 16+ locally, create a database, then configure the connection via
.NET User Secrets (never commit real credentials):

```bash
cd Silver-Task.Server
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=silvertask_dev;Username=postgres;Password=postgres"
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 48)"
cd ..
dotnet tool restore   # pins dotnet-ef per .config/dotnet-tools.json
dotnet ef database update --project Silver-Task.Server --startup-project Silver-Task.Server
```

**Running it — Option A (Visual Studio):** open `Silver-Task.slnx`, press F5 with
`Silver-Task.Server` as the startup project. The SPA proxy launches the Vite dev server
automatically.

**Running it — Option B (CLI):**

```bash
cd Silver-Task.Server
dotnet run   # auto-launches the Vite dev server via SpaProxy
```

The app is served at `https://localhost:7001` (API) / `https://localhost:42665` (SPA dev server,
proxied — browser only ever talks to one origin). To run the frontend standalone against an
already-running API: `cd silver-task.client && npm run dev`.

To add a new migration after changing an entity or configuration:

```bash
dotnet ef migrations add <MigrationName> --project Silver-Task.Server --startup-project Silver-Task.Server --output-dir Data/Migrations
```

## Configuration

ASP.NET Core does not read `.env` files directly. `.env.example` at the repo root documents
variable names/shapes for reference; real values go through **.NET User Secrets** in development
or **environment variables** in production (`__` as the config-section separator — e.g.
`Jwt:Secret` becomes `Jwt__Secret`). The Debian installer generates
`/etc/silvertask/silvertask.env` for you (see `deploy/silvertask.env.example` for the template it's
based on) and wires it into the systemd service via `EnvironmentFile=`.

| Variable | Purpose | Required? |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | Yes |
| `Jwt__Secret` | JWT signing key, 32+ random bytes (`openssl rand -base64 48`) — app throws at startup if unset | Yes |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__ExpiryMinutes` | JWT validation/expiry (non-secret defaults already in `appsettings.json`) | No |
| `Cors__AllowedOrigins__0` | Your production origin (e.g. `https://tasks.example.com`) — empty by default, which allows nothing cross-origin | Recommended |
| `General__ApplicationBaseUrl` | Base URL email links are built against — falls back to the first CORS origin if unset | Recommended if using email |
| `Attachments__StorageRoot` | Directory for uploaded files — see [File Storage](#file-storage) | No (has a default) |
| `Smtp__Host`, `Smtp__Port`, `Smtp__EnableSsl`, `Smtp__Username`, `Smtp__Password`, `Smtp__FromAddress`, `Smtp__FromName` | Outgoing email — see [Email Configuration](#email-configuration) | No — app is fully functional without email |
| `ForwardedHeaders__KnownProxies__0` (and `__1`, `__2`, ...) | Only needed if your reverse proxy is NOT on the same host as the app (defaults to trusting loopback only) | No |
| `Maintenance__FlagFile` | Path checked by `MaintenanceModeMiddleware` — see [Upgrade Engine](#upgrade-engine) → "Activation." Empty/unset disables maintenance mode entirely | No — `install-debian.sh` sets it automatically |
| `Upgrade__Channel` | Release channel (`stable` or `beta`) for `scripts/update-debian.sh --check/--status/--latest/--target-version` — see [Upgrade Engine](#upgrade-engine) → "Release channels." An explicit `--channel` flag overrides this | No — defaults to `stable` |
| `Upgrade__MaintenanceWindow` | `HH:MM-HH:MM` (server-local time) restricting when `--activate`/`--rollback` may run — see [Upgrade Engine](#upgrade-engine) → "Maintenance window policy" | No — unset means no restriction |
| `Diagnostics__DbLatencyDegradedMs` | Database round-trip time (ms) above which `GET /api/admin/diagnostics` reports `database.status` as `degraded` — see [docs/monitoring-runbook.md](docs/monitoring-runbook.md) | No — defaults to `1000` |
| `Diagnostics__DiskFreePercentDegraded` | Free-space percentage on the attachments storage drive below which `diskSpace.status` is `degraded` | No — defaults to `10` |
| `Diagnostics__WorkerStaleMultiplier` | A background worker is `degraded` once its last successful tick is older than its own interval times this multiplier | No — defaults to `3` |
| `Security__LoginRateLimit__PermitLimit` | Max `POST /api/auth/login` attempts per client IP per window — see [Security](#security) → "Security hardening" | No — defaults to `10` |
| `Security__LoginRateLimit__WindowSeconds` | The window (seconds) the above limit applies over | No — defaults to `60` |

## Database

PostgreSQL, accessed exclusively through Entity Framework Core (Npgsql provider) — the API layer
is the only thing that ever talks to it. All primary keys are `uuid`; enums (`Status`, `Priority`,
`Role`, custom field `FieldType`, notification types, etc.) are stored as `varchar` rather than
native Postgres enum types, so adding a new value later is a plain data migration, never an `ALTER
TYPE`. 51 tables as of v1.0.1, spanning core task management, custom fields (EAV), notifications,
email delivery, automations, templates, saved views, and reporting — see
`Silver-Task.Server/Data/AppDbContext.cs` for the authoritative table list and
`Data/Configurations/*.cs` for every relationship/index.

**Migrations** are the only supported way to change schema — the Debian installer runs
`dotnet ef database update` automatically (idempotent: only applies what's not already recorded in
`__EFMigrationsHistory`, never resets or drops existing tables). To apply manually:

```bash
dotnet ef database update --project Silver-Task.Server --startup-project Silver-Task.Server
```

This has been verified against both an already-populated database (upgrade path) and a completely
empty one (clean-install path) — see [Release Information](#release-information).

## File Storage

Uploaded attachments are stored on **local disk**, outside `wwwroot`, so they're never directly
web-accessible by URL — every read goes through the authorized `GET /api/attachments/{id}/download`
endpoint. Filenames on disk are always server-generated GUIDs; the original client-supplied
filename is kept only in the database (for display and the download's `Content-Disposition`
header), which avoids path traversal without needing to sanitize an arbitrary filename into
something safe to use as a path.

- **Development default**: `Silver-Task.Server/App_Data/attachments` (relative to the content root).
- **Debian installer default**: `/var/lib/silver-task/attachments`, owned by the `silvertask`
  service account, mode `750` — deliberately outside the source/publish tree so files survive
  [updates](#updating) (which replace the publish directory) cleanly.
- Configurable via `Attachments__StorageRoot`; a 25MB per-file size cap and a small blocklist of
  dangerous executable extensions are enforced server-side regardless of location.
- **Must be backed up together with the database** — the `Attachments` table is the source of
  truth for filenames/metadata, the directory is the source of truth for bytes; see
  [Backup](#backup).

## Authentication

Cookie-based JWT: on login the API issues a JWT and sets it as an **httpOnly, Secure,
SameSite=Strict cookie** (`silvertask_auth`) rather than returning it in the response body, keeping
it out of reach of JavaScript (so an XSS bug can't exfiltrate it). Passwords are hashed with ASP.NET
Core's `PasswordHasher<User>` (PBKDF2) — never stored, logged, or returned in any API response.
Authorization is secure-by-default: any endpoint without an explicit `[Authorize]`/`[AllowAnonymous]`
requires authentication via a global `FallbackPolicy`, so a new controller can't accidentally ship
unauthenticated. `POST /api/users` (registration) is open only while the `Users` table is empty —
that first account is always created as Administrator; once any user exists, creating more requires
an authenticated Administrator. See [Security](#security) for the full audit history of this
subsystem.

## Email Configuration

Fully optional — Silver Task is completely functional with email left unconfigured (email
notifications are simply off; nothing else depends on it). To enable it, set the `Smtp__*`
environment variables (any standard SMTP provider — Microsoft 365, Google Workspace, SendGrid SMTP
relay, etc.) via the Debian installer's prompts/flags or directly in
`/etc/silvertask/silvertask.env`, then restart: `sudo systemctl restart silvertask`.

Once configured, an Administrator can verify it without risking a real send to end users: **Admin →
Email → Send Test Email**. Admin → Email also shows SMTP status (configured yes/no — never the
host/credentials themselves), lets you customize the 5 notification + 2 digest email templates
(controlled `{{Variable}}` substitution only, no code execution), and provides a read-only delivery
log (status/type/timestamps only — no email body, no raw address) for diagnosing delivery problems.
See [Appendix](#appendix-feature--phase-implementation-notes) for the full email/digest
architecture.

## Background Workers

Background processing is **in-process**, not a separate deployable — 7 workers (email delivery +
retry, daily/weekly digest scheduling, due-date notifications, recurring task generation,
automation execution, automation overdue checks, notification retention cleanup) start
automatically the moment the app itself starts, registered in `Program.cs`. There is nothing extra
to install or start for background jobs to work — but this also means **running more than one copy
of the application process runs duplicate copies of every background worker** (there is no
distributed locking in this codebase — see [Known limitations](#known-limitations)). The systemd
service the installer creates runs exactly one instance, with `Restart=always` so a crashed process
comes back automatically; do not scale this service horizontally without adding distributed
coordination first.

The 6 interval-driven workers (everything above except automation execution, which is event-driven)
each report a heartbeat after every successful tick — see
[docs/monitoring-runbook.md](docs/monitoring-runbook.md) for how to read that via
`GET /api/admin/diagnostics` and what a stale worker means.

## Updating

```bash
sudo ./scripts/update-debian.sh
```

1. Confirms an existing installation is present.
2. **Backs up first** (via `scripts/backup-debian.sh`) and refuses to continue if the backup fails
   — see [Backup](#backup). Skippable with `--skip-backup`, not recommended.
3. `git fetch`/`checkout`/`pull`s the latest source in place (or a specific ref: `--ref=v1.0.1`).
4. Rebuilds (`dotnet publish -c Release`) — if the build fails, the **previous build is restored**
   and the running service is left untouched.
5. Runs database migrations (idempotent, additive-only).
6. Restarts the systemd service and runs a real health check against the app's internal port.
7. Reports success or failure with a non-zero exit code on failure.

Your `.env` file, database, and uploaded files are never touched by an update beyond the migration
step itself (which only ever adds/alters schema, never resets data).

## Upgrade Engine

Phase 52 added a second, additive command surface to the same `scripts/update-debian.sh` — a CLI
upgrade engine that **discovers, validates, and stages** a target release; Phase 53 added the
safety layer that must exist before anything can be activated: a verified pre-upgrade backup, a
persistent-data placement check, and migration discovery/validation/planning; Phase 54 added
**activation itself** — `--activate` is the command that actually replaces the running application,
runs the migration, and commits the new installed version; **Phase 55 added the undo path** —
`--rollback` reactivates the preserved previous release (restoring the pre-upgrade database backup
too, if and only if that upgrade actually required a migration) with the same
maintenance-mode/health/version-validation discipline as activation. **Phase 56 adds release
management on top** — an opt-in pre-release channel, an opt-in maintenance-window policy, a durable
release history, and a read-only preflight check — none of it changing any existing default
behavior; see [Release channels, maintenance window & preflight](#release-channels-maintenance-window--preflight)
below. This is deliberately **not** a
replacement for the plain `sudo ./scripts/update-debian.sh` above, which still works exactly as it
always has for a single-step update. The upgrade engine splits the same work into explicit,
separately-confirmed steps — **prepare** (`--latest`/`--target-version`, stops at `READY FOR
ACTIVATION`), **activate** (`--activate`), and, if needed, **roll back** (`--rollback`) — so a
validated, backed-up release can be reviewed before anything live is touched, and undone safely if
it turns out to be bad. See [docs/upgrade-safety.md](docs/upgrade-safety.md) for the prepare-stage
workflow and persistent-data inventory, [docs/upgrade-activation.md](docs/upgrade-activation.md)
for the full activation workflow, [docs/rollback.md](docs/rollback.md) for the full rollback
workflow and its data-loss warnings, the two
[operator](docs/upgrade-operator-checklist.md) [checklists](docs/rollback-operator-checklist.md)
for concise runbooks, and [docs/restore.md](docs/restore.md) for verifying/restoring a backup.

```bash
sudo ./scripts/update-debian.sh --check                        # is a newer stable release available?
sudo ./scripts/update-debian.sh --status                       # upgrade/rollback/version status report
sudo ./scripts/update-debian.sh --latest                       # validate, back up, and stage the latest stable release
sudo ./scripts/update-debian.sh --target-version 1.1.0         # validate, back up, and stage a specific stable release
sudo ./scripts/update-debian.sh --dry-run --latest              # show what --latest would do; changes nothing
sudo ./scripts/update-debian.sh --activate                     # activate a prepared (READY_FOR_ACTIVATION) release
sudo ./scripts/update-debian.sh --activate --yes                # activate without the confirmation prompt
sudo ./scripts/update-debian.sh --rollback                     # roll back to the previously active release
sudo ./scripts/update-debian.sh --rollback --dry-run             # show the rollback plan; changes nothing
sudo ./scripts/update-debian.sh --channel=beta --latest          # opt in to pre-release releases for this check
sudo ./scripts/update-debian.sh --history                       # show past activation/rollback history
sudo ./scripts/update-debian.sh --doctor                        # preflight-check the toolchain (read-only)
sudo ./scripts/update-debian.sh --help
```

**`--check`** and **`--status`** are read-only — they never write a lock, state, or log-of-mutation
entry, and never touch application files, the database, or services. `--check` compares the
installed version against the newest stable git tag; `--status` additionally reports installed-vs-
running version consistency and whatever the upgrade engine last did (see "Upgrade state" below).

**`--latest`** / **`--target-version X.Y.Z`** (mutually exclusive) resolve a target release, then:
validate installed/running version consistency, discover available stable releases, reject a
same-version or older ("downgrade") target, validate the release's metadata (see "Release
metadata" below), check local disk space, ask for confirmation (`[y/N]`, never defaulted to yes —
skip with `--yes`), acquire the upgrade lock, fetch the target tag and stage it into an isolated
`git worktree` under `$SILVERTASK_INSTALL_DIR/upgrade-staging/<version>` (a second checkout that
shares git objects with the main install but never touches `$SILVERTASK_INSTALL_DIR/source` — what
the plain `update-debian.sh` above actually builds from — or the running application), then run the
full safety pipeline below (persistent-data check → backup → backup verification → migration
validation → migration planning) before reporting `READY FOR ACTIVATION`.

**`--dry-run`** (with `--latest`/`--target-version`) runs every validation step and reports the
same plan, but never fetches new git tags, never creates a worktree, never creates a backup, never
acquires the upgrade lock, and never writes upgrade state — the only side effect is an
informational log line. Its persistent-data check *does* run for real (it's pure read-only path
comparison), and its disk-space-for-backup check reports real numbers; its migration plan section
honestly reports "not available in dry-run" rather than fabricating one (generating a real plan
requires a staged, built worktree, which dry-run never creates).

**Pre-upgrade backup**: before touching anything, `--latest`/`--target-version` create and verify a
full backup — database (`pg_dump`, custom format), attachments, and configuration — by invoking the
existing `scripts/backup-debian.sh` (see [Backup](#backup) below) tagged `pre-upgrade` and linked to
this attempt's upgrade ID. **A failed or unverifiable backup blocks the upgrade outright** — the
engine never proceeds to staging/migration work on an unbacked-up installation. Verification is
structural, not just "the file exists": `pg_restore --list` actually reads the dump's table of
contents, and the configuration copy is checked for its required keys (values are never printed or
logged either way).

**Persistent data check**: confirms `Attachments__StorageRoot` isn't nested inside anything an
upgrade replaces or discards (`$SILVERTASK_INSTALL_DIR/source`, `.../publish`, or
`.../upgrade-staging`) — the standard install location
(`/var/lib/silver-task/attachments`) already passes cleanly; this only fires on genuine
misconfiguration, and blocks with a specific remediation message rather than proceeding. See
[docs/upgrade-safety.md](docs/upgrade-safety.md) for the full persistent-data inventory.

**Migration validation & planning**: uses the project's own EF Core CLI (`dotnet-ef`, pinned in
`.config/dotnet-tools.json`) as the sole authority — never a second migration mechanism, and never
executes one. `dotnet ef migrations list` against the *currently installed* code (a real, read-only
database connection — the same read `dotnet ef database update` itself relies on) confirms the
running installation's own migrations are fully applied before planning anything new; `dotnet ef
migrations list --no-connect` against the *staged* release (zero database connection) validates its
migrations are well-formed with no duplicates; the difference between the two lists is the pending-
migration plan, and `dotnet ef migrations script --idempotent` (also zero database connection)
generates the actual SQL as a real file (`migration-plan.sql`, next to the staged release) for
review — nothing is ever applied. Each upgrade is classified `SAFE` / `REQUIRES_BACKUP` /
`REQUIRES_MAINTENANCE_MODE` (see [docs/upgrade-safety.md](docs/upgrade-safety.md)).

**Upgrade ID**: every `--latest`/`--target-version` attempt (not `--dry-run`) gets a unique ID
(`upgrade-<timestamp>-<random>`) linking its upgrade state, pre-upgrade backup manifest, and log
entries together — see the manifest's `"upgradeId"` field and `docs/restore.md` → "Finding the
right backup."

**Release discovery**: stable releases are discovered from the repository's own git tags
(`git ls-remote --tags` against the configured remote — never a second repository, never a URL
taken from the command line). Only tags matching `vMAJOR.MINOR.PATCH` exactly are considered — pre-
release tags (`v1.1.0-beta`, `v1.1.0-rc1`) and branch-like refs (`main`, `latest`, `development`)
are excluded outright, not just deprioritized, and versions are sorted numerically (`1.9.0` <
`1.10.0` < `2.0.0`), never alphabetically.

**Release metadata** — `releases/<version>.json` in this repository, read directly from the target
git tag (works even before a worktree is staged):
```json
{
  "version": "1.1.0",
  "channel": "stable",
  "minimumSupportedVersion": "1.0.0",
  "requiresDatabaseMigration": false,
  "requiresDataMigration": false,
  "requiresRestart": true
}
```
Not every historical release needs one — `releases/1.0.1.json` is a real example; `v1.0.0` has none
and falls back to documented safe defaults (a migration is conservatively assumed possible, a
restart is assumed needed, no minimum-version floor). A present-but-malformed file (wrong
`"version"`, an unsupported `"channel"`, or an invalid `minimumSupportedVersion`) blocks the
upgrade outright rather than being silently ignored; a well-formed `minimumSupportedVersion` that
the installed version doesn't satisfy also blocks it (with a clear message), preparing the ground
for future releases to declare "you must upgrade through an intermediate version first" without
Phase 52 needing to build a full compatibility matrix for it.

**Upgrade lock**: only one upgrade operation (prepare *or* activate) can run at a time, enforced
with `flock` on `/var/lock/silvertask-upgrade.lock` — an OS-held lock tied to the process, so a
crashed/killed attempt releases it automatically and can never permanently block a future one.
`--activate` acquires its own fresh instance of this same lock (the original prepare-time lock is
already released by the time `--latest`/`--target-version` finishes) — activation is its own
exclusive operation, not a resumption of the earlier one. `--status` reports `IN PROGRESS` (with
target/start time, and whether maintenance mode is also active) while the lock is actually held.

**Upgrade state**: `$SILVERTASK_INSTALL_DIR/upgrade-state.json` records the most recent upgrade
attempt — upgrade ID, previous/target version, status, step, timestamps, per-stage status for the
backup/persistent-data/migration-planning pipeline (Phase 53), and per-stage status for
activation/maintenance/migration-execution/service/health/version/smoke-test (Phase 54) — for
`--status` to report. Left in place after success, failure, or a crash, and only ever overwritten
by the next real attempt, never auto-deleted. Status values: `IDLE`, `CHECKING`, `VALIDATING`,
`PREPARING`, `CHECKING_PERSISTENT_DATA`, `BACKING_UP`, `VERIFYING_BACKUP`, `PLANNING_MIGRATIONS`,
`READY_FOR_ACTIVATION`, `ACTIVATING`, `MAINTENANCE_ENABLED`, `MIGRATING`, `STARTING_SERVICES`,
`FAILED`, `COMPLETED` — `COMPLETED` is only ever written once activation has fully succeeded,
including the post-restart health/version/smoke validation. Rollback attempts get their own,
separate `$SILVERTASK_INSTALL_DIR/rollback-state.json` (status values `ROLLBACK_REQUESTED` through
`ROLLBACK_COMPLETED`/`ROLLBACK_FAILED`) — deliberately not merged into the same file, since
`--status` reports "Last Upgrade" and "Last Rollback" as two independent facts, matching the actual
independent history (an upgrade and a later rollback of it are two different events).

**Upgrade log**: `/var/log/silver-task/upgrade.log` — a structured, timestamped history of every
upgrade-engine invocation, separate from the installer's own `/var/log/silver-task-install.log`.
Never contains secrets/env vars/credentials, same discipline as every other log this project
writes.

### Activation

`sudo ./scripts/update-debian.sh --activate` is the one command that actually replaces the running
application. It requires a prior `--latest`/`--target-version` to have already left the
installation `READY_FOR_ACTIVATION` (every safety-pipeline stage recorded `OK`, the staged release
and its backup still present on disk) — otherwise `UPGRADE ACTIVATION BLOCKED` (exit `17`) with the
specific missing prerequisite. After a confirmation panel (current/target version, upgrade ID,
backup verification, whether a migration is required — `[y/N]`, never defaulted to yes, skip with
`--activate --yes`), it: builds the target release into a **fresh** directory before touching
anything live (a build failure here leaves the old application completely untouched) → enables
**maintenance mode** → stops the service → swaps the publish directory in (the previous one is
renamed aside, never deleted) → runs `dotnet ef database update` (the project's own migration
command, nothing invented) → starts the service → polls `/api/health/ready` with bounded retries →
validates the running version matches the target exactly (backend authoritative; a best-effort
check also looks for the version string in the served frontend bundle) → runs smoke tests (SPA
shell reachable — no authenticated calls, since no service account exists to make one safely) →
**only now** commits `installed-version.json` → disables maintenance mode → one final availability
check with normal traffic restored. See [docs/upgrade-activation.md](docs/upgrade-activation.md)
for the full step-by-step breakdown, and [docs/upgrade-operator-checklist.md](docs/upgrade-operator-checklist.md)
for a concise runbook.

**Maintenance mode**: a small ASP.NET Core middleware (`MaintenanceModeMiddleware`) checks for a
flag file (`Maintenance__FlagFile`, default `/opt/silver-task/maintenance.json` — deliberately
outside the publish directory so it survives the swap) on every request; while present, everything
except `GET /api/health*` returns `503` with `Retry-After: 30` and a generic message — never the
upgrade ID/target version/any internal detail, which stay server-side only. No nginx reload is
needed; it works whether the old or new binary is the one currently running.

**Failure handling**: every failure path prints a recovery-information block (previous/target
version, upgrade ID, backup location, an explicit "version X.Y.Z has NOT been marked installed")
and records a specific failure state — `ACTIVATION_FAILED`, `MAINTENANCE_MODE_FAILED`,
`SERVICE_START_FAILED`, `MIGRATION_FAILED`, `HEALTH_CHECK_FAILED`, `VERSION_VALIDATION_FAILED`,
`SMOKE_TEST_FAILED` — not a generic error. **No automatic rollback exists** — a failure after
maintenance mode was enabled leaves it enabled (deliberately: a clear maintenance response is safer
than possibly-broken traffic) for manual investigation; the pre-upgrade backup and the preserved
`<publish-dir>.previous` are what a human uses to recover, per
[docs/restore.md](docs/restore.md).

**Interrupted upgrade detection**: if the server reboots or the activation process is killed
mid-way, `sudo ./scripts/update-debian.sh --status` detects it on the next run — a maintenance flag
still active with no process holding the upgrade lock is unambiguous evidence — and reports
`INTERRUPTED UPGRADE DETECTED` (exit `25`), distinct from the lighter-weight `STALE UPGRADE LOCK
DETECTED` Phase 53 already reports for an interrupted *prepare* (where maintenance mode was never
touched). Neither is auto-resumed or auto-cleaned-up.

### Rollback

`sudo ./scripts/update-debian.sh --rollback` undoes a failed, interrupted, or unwanted activation —
the very last one, using the exact record `--activate` itself wrote (`upgrade-state.json`'s
`activationStatus`/`migrationRequired`/`backupDir` fields — never a second "release history"
mechanism). Eligibility requires that the last recorded activation actually switched the release
(`activationStatus == OK`) and that its preserved `<publish-dir>.previous` and pre-upgrade backup
are both still on disk — otherwise `ROLLBACK BLOCKED` (exit `26`) or, specifically, `ROLLBACK
TARGET UNAVAILABLE` (exit `27`). It then shows a plan (current/target version, related upgrade ID,
numbered steps, database-restore decision) and requires `[y/N]` confirmation — with a **second**,
stronger confirmation (type the exact target version) if a database restore is required, since
that's destructive.

**Application-only vs. database-restore rollback** is decided from the same `migrationRequired`
flag the failed upgrade already recorded — reused directly, never a new compatibility matrix:
`false` → `APPLICATION_ONLY_ROLLBACK` (just the release switches back); `true` →
`DATABASE_RESTORE_REQUIRED` (the release switches back *and* the pre-upgrade backup is restored);
anything missing/corrupted → `MANUAL_RECOVERY_REQUIRED`, which **blocks** the rollback rather than
guessing (exit `26`).

**Emergency backup**: before any database restore, the *current* (post-failure) database is backed
up first — tagged `emergency-pre-rollback`, via the same `scripts/backup-debian.sh` mechanism and
verification every other backup in this project uses. A failed/unverified emergency backup blocks
the rollback (exit `28`); the escape hatch `--force-no-emergency-backup` requires its own typed
confirmation and isn't recommended.

**Database restore** uses `pg_restore --clean --if-exists --no-owner` against the pre-upgrade
backup — the same connection credentials `backup-debian.sh` used to create it, run in reverse — then
validates the restored schema against the rollback target's own code (reusing the exact migration-
state check `--latest`/`--activate` already use) before proceeding; a restore that "succeeds" but
produces an incompatible database is still `DATABASE_RESTORE_FAILED` (exit `29`).

**Configuration rollback** is opt-in (`--restore-config`) and skipped by default: `--activate` never
modifies `silvertask.env`, so there's nothing an automated upgrade could have changed for rollback
to automatically detect. When used, the current configuration is emergency-copied first, then the
pre-upgrade backup's copy is restored over it.

**Health/version/smoke validation and version commit** follow the identical sequence and discipline
`--activate` already uses — the installed version is committed only after all of them pass.

**Interrupted rollback detection**: `--status` distinguishes an interrupted rollback (maintenance
flag active, no lock held, and the flag's own recorded ID is prefixed `rollback-`) from an
interrupted activation — `INTERRUPTED ROLLBACK DETECTED`, exit `34`, vs. the existing exit `25`.
Neither is auto-resumed.

See [docs/rollback.md](docs/rollback.md) for the full workflow, its explicit data-loss warning, and
manual-recovery instructions, and [docs/rollback-operator-checklist.md](docs/rollback-operator-checklist.md)
for a concise runbook.

**Exit codes**:

| Code | Meaning |
|---|---|
| 0 | Success / no blocking problem (including a deliberate no-op: already on target, or declined a confirmation prompt) |
| 1 | General error |
| 2 | Invalid arguments |
| 3 | Version inconsistency (installed ≠ running, or unknown) |
| 4 | Target version unavailable |
| 5 | Unsupported upgrade path (downgrade, or a release's `minimumSupportedVersion` not met) |
| 6 | Upgrade/rollback already in progress |
| 7 | Repository access failure |
| 8 | Insufficient disk space (release staging, or — same code, checked again later — backup) |
| 9 | Database backup failed |
| 10 | Database backup verification failed |
| 11 | Configuration backup failed |
| 12 | Configuration backup verification failed |
| 13 | Persistent data safety check failed |
| 14 | Current database migration state invalid |
| 15 | Target release migration validation failed |
| 16 | Migration planning failed |
| 17 | Activation prerequisites missing |
| 18 | Maintenance mode failure |
| 19 | Application activation/release-switch failure (checkout/build/directory swap) |
| 20 | Service startup failure |
| 21 | Migration execution failure |
| 22 | Health check timeout |
| 23 | Version validation failure |
| 24 | Smoke test failure |
| 25 | Interrupted upgrade detected (`--status` only) |
| 26 | Rollback eligibility failed (includes `MANUAL_RECOVERY_REQUIRED`) |
| 27 | Rollback target unavailable (no preserved previous release) |
| 28 | Emergency backup failed |
| 29 | Database restore (or its post-restore validation) failed |
| 30 | Configuration restore failed |
| 31 | Rollback service startup failed |
| 32 | Rollback health check failed |
| 33 | Rollback version validation failed |
| 34 | Interrupted rollback detected (`--status` only) |
| 35 | Blocked by maintenance-window policy (`--override-maintenance-window` to proceed anyway, with confirmation) |
| 36 | Preflight (`--doctor`) check failed |
| 37 | Invalid or disallowed release channel for the requested operation |

(Codes 6/18/19/24 are reused for rollback's lock/maintenance/release-switch/smoke-test failures —
the same categories already defined for activation, not redefined.)

**Current limitations**: rollback only covers the single most recent activation (one
`<publish-dir>.previous`/backup slot, not a multi-generation history) and does not auto-retry or
auto-recover from its own failure — a failed rollback requires administrator review, per
[docs/rollback.md](docs/rollback.md) "Manual recovery." There is no web-based upgrade UI, and none
is planned as part of this CLI-only design — upgrade administration always requires root/sudo on
the host itself. Disk-space checking remains an approximation (precise sizing is future work). The
legacy `sudo ./scripts/update-debian.sh` (no flags, or `--ref=`/`--skip-backup`) continues to work
exactly as before and remains a valid single-step alternative to prepare-then-activate.

### Release channels, maintenance window & preflight

> Inferred scope (Phase 56) — see [docs/release-management.md](docs/release-management.md) for the
> full design rationale.

**Release channels** (`--channel=stable|beta`, or `Upgrade__Channel` in `silvertask.env`) select
which git tags `--check`/`--status`/`--latest`/`--target-version` consider. **`stable` is always the
default** — it is byte-identical to the pre-Phase-56 behavior described above (only `vMAJOR.MINOR.PATCH`
tags). `beta` additionally surfaces pre-release tags (`v1.1.0-beta`, `v1.1.0-rc1`); a pre-release
version string is only ever accepted as a `--target-version` when the effective channel is `beta` —
on the default `stable` channel it's rejected outright, even as an exact string, so a typo or
copy-pasted pre-release tag can never slip through unnoticed. `cmd_prepare` additionally cross-checks
the *resolved release's own declared* `releases/<version>.json` `"channel"` field against the
effective operating channel — a release whose metadata declares `"channel": "beta"` can never be
selected while operating on `stable`, even via an exact `--target-version` (exit `37` on mismatch).

**Maintenance window** (`Upgrade__MaintenanceWindow` in `silvertask.env`, e.g. `02:00-04:00`,
server-local time, wrapping midnight supported) is **opt-in and unset by default** — with it unset,
`--activate`/`--rollback` behave exactly as before, at any time of day. When set, both commands check
the current time against the window *after* the existing `[y/N]` confirmation and *before* acquiring
the upgrade lock; outside the window the operation is blocked (`UPGRADE`/`ROLLBACK BLOCKED BY
MAINTENANCE WINDOW POLICY`, exit `35`) unless `--override-maintenance-window` is also passed, which
requires its own typed confirmation (the same `st_confirm_destructive` mechanism the database-restore
step already uses) — never a silent bypass.

**Release history** (`--history [--limit=N]`, default 20, most recent first) is a durable,
append-only log of every `--activate`/`--rollback` this installation has completed —
`$SILVERTASK_INSTALL_DIR/release-history.jsonl`, one compact JSON object per line (JSON Lines), so
appending never requires rewriting the file the way `upgrade-state.json`/`rollback-state.json`
(which each hold only the single most recent attempt) would. Only real terminal outcomes are
recorded (`COMPLETED`/`FAILED`, from `--activate`/`--rollback` themselves) — `--check`/`--status`/
`--latest`/`--target-version` never write to it, since they never change anything.

**Preflight check** (`--doctor`, read-only, modifies nothing) verifies the host is actually ready to
run an upgrade before you try one: required tools on `PATH` (`git`, `dotnet`, `pg_dump`,
`pg_restore`, `curl`, `openssl`), `dotnet-ef` restorable, the environment file present with its
required keys, installed/running version consistency, whether the upgrade lock or maintenance mode
is currently (possibly stuck) active, the maintenance-window policy's current state, and disk space.
Each check prints `PASS`/`WARN`/`FAIL`; only a `FAIL` blocks (exit `36`) — a held lock or active
maintenance mode is reported as `WARN`, since it may simply mean an upgrade is legitimately in
progress right now.

### Automated upgrade testing & release certification

> Inferred scope (Phase 57) — see [docs/release-certification.md](docs/release-certification.md)
> for the full design rationale.

`scripts/certify-release.sh` is a **separate top-level script**, not another `update-debian.sh`
mode — it orchestrates a full **install → upgrade → validate → rollback → validate** lifecycle
test using the existing `install-debian.sh`/`update-debian.sh`/`uninstall-debian.sh` exactly as an
operator would run them by hand, producing a durable certification report to check before tagging
a release for the stable channel. **Certification is report-only** — it never gates anything in
the runtime upgrade engine automatically.

```bash
sudo ./scripts/certify-release.sh --candidate=1.2.0 --disposable-host-confirmed
```

**⚠️ Disposable hosts only.** This script installs, upgrades, rolls back, and (with `--cleanup`)
uninstalls Silver Task on whatever host it runs on — it requires an explicit
`--disposable-host-confirmed` flag (no default) plus, unless `--yes` is passed, a typed
confirmation. Never run it against a host that serves real users or holds real data.

Each of the 7 required stages (baseline install, baseline health, candidate prepare, candidate
activate, candidate validate, rollback, rollback validate) — plus an optional `cleanup` stage —
records `PASS`/`FAIL`/`SKIPPED` to a JSON Lines report
(`$SILVERTASK_CERTIFICATION_DIR/certification-<candidate>-<certId>.jsonl`, same append-only
one-line-per-event shape as Phase 56's `release-history.jsonl`). The verdict (`CERTIFIED`/
`NOT_CERTIFIED`) is a separate small exit-code scheme (`0`–`9`) from `update-debian.sh`'s own,
since it's a distinct process orchestrating that script rather than another mode of it — see
[docs/release-certification.md](docs/release-certification.md) for the full stage-by-stage
breakdown and exit code table.

## Backup

```bash
sudo ./scripts/backup-debian.sh                       # backs up to /var/backups/silver-task, keeps last 7
sudo ./scripts/backup-debian.sh --keep=30              # keep the last 30 backup sets instead
sudo ./scripts/backup-debian.sh --max-age-days=90      # also delete anything older than 90 days
```

Each run creates a timestamped set containing: a `pg_dump` (custom format) of the database, a
`tar.gz` of the file storage directory, and a copy of the environment file (permissions restricted
to root — it contains secrets, and the backup log never records their values). Beyond "the file
exists and is non-empty," the database dump is verified with `pg_restore --list` (a real structural
read of its table of contents) and the configuration copy is checked for its required keys —
verification failure blocks the whole run rather than reporting a false success. Every run also
writes `manifest.json` into the backup set (type, timestamps, and — when triggered by the [upgrade
engine](#upgrade-engine) — the upgrade ID and installed/target versions; never any secret value) —
see [docs/restore.md](docs/restore.md) → "Finding the right backup."

Retention (`--keep`/`--max-age-days`) only deletes directories matching this script's own
`YYYYMMDD-HHMMSS` naming, never the single newest backup, and never a backup still linked to an
in-progress upgrade (status other than `FAILED` in `upgrade-state.json`) — see
`st_assert_safe_backup_dir`/`st_is_backup_set_name` in `scripts/lib/common.sh`.

The upgrade engine (`--latest`/`--target-version`, above) calls this same script internally
(tagged `pre-upgrade`, linked to its upgrade ID) — there is only one backup implementation in this
project, reused everywhere a backup is needed.

Run this on a schedule via cron or a systemd timer, e.g. nightly:

```bash
echo "0 3 * * * root /opt/silver-task/source/scripts/backup-debian.sh >> /var/log/silver-task-backup.log 2>&1" | sudo tee /etc/cron.d/silver-task-backup
```

## Restore

**There is no automated restore script** — restoring is rarer and higher-stakes than backing up, so
it's deliberately a manual, reviewable set of commands rather than something a script runs
unattended. This is the production restore procedure; to just verify a backup is good (including
every pre-upgrade backup the [upgrade engine](#upgrade-engine) creates) without any risk to
production, see [docs/restore.md](docs/restore.md) → "Restoring into an isolated test database"
instead:

1. **Stop the app**: `sudo systemctl stop silvertask` (avoids writes to a database mid-restore).
2. **Restore the database** into either the existing database (if you're recovering from
   corruption/bad data) or a fresh one (if rebuilding a whole server):
   ```bash
   # Fresh database:
   sudo -u postgres createdb silvertask_restored
   sudo -u postgres pg_restore -d silvertask_restored /var/backups/silver-task/<timestamp>/database.dump
   # Then update ConnectionStrings__DefaultConnection in /etc/silvertask/silvertask.env to point at it.
   ```
   ⚠️ Restoring **over** an existing database's current data is destructive and cannot be undone —
   take a fresh backup of the *current* state first if there's any chance you'll want it back.
3. **Restore uploaded files** to the same `Attachments__StorageRoot` path referenced by the restored
   database's generation of backups (mismatched generations mean orphaned files or broken links —
   restore both from the *same* timestamped backup set):
   ```bash
   sudo tar -xzf /var/backups/silver-task/<timestamp>/attachments.tar.gz -C /var/lib/silver-task/
   ```
4. **Restore configuration** (only if the current `/etc/silvertask/silvertask.env` was lost too —
   otherwise leave the current one in place so you don't revert to old secrets/settings):
   ```bash
   sudo cp /var/backups/silver-task/<timestamp>/silvertask.env /etc/silvertask/silvertask.env
   sudo chmod 640 /etc/silvertask/silvertask.env
   ```
5. **Restart and verify**: `sudo systemctl start silvertask`, then `curl -f
   http://127.0.0.1:5000/api/health/ready` and a real login + spot-check that the restored tasks/
   projects/files are actually there before resuming normal use.

This exact procedure (dump → restore into a scratch database → verify row counts and foreign-key
counts match) was performed for real during Phase 47's release audit — see
[Release Information](#release-information).

## Troubleshooting

**Check application status:**
```bash
sudo systemctl status silvertask
sudo journalctl -u silvertask -f          # live logs
sudo journalctl -u silvertask -n 200      # last 200 lines
```

**Check nginx:**
```bash
sudo systemctl status nginx
sudo nginx -t                              # validate config
sudo tail -f /var/log/nginx/error.log
```

**Check the database:**
```bash
sudo systemctl status postgresql
sudo -u postgres psql -l                   # list databases
sudo -u postgres psql -d silvertask -c "SELECT count(*) FROM \"Users\";"
```

**Check disk space / memory:**
```bash
df -h /opt /var/lib/silver-task /var/backups
free -h
```

**Check ports:**
```bash
sudo ss -ltnp | grep -E ':80|:443|:5000|:5432'
```

**Check HTTPS:**
```bash
curl -vI https://your-domain/api/health
sudo certbot certificates                  # if using Let's Encrypt
```

**Application health directly:**
```bash
curl http://127.0.0.1:5000/api/health         # liveness — process is up
curl http://127.0.0.1:5000/api/health/ready   # readiness — database is reachable too
```

### Common errors

| Symptom | Likely cause / fix |
|---|---|
| **Port already in use** during install | Another service is bound to 80/443. `sudo ss -ltnp \| grep :80` to find it, then either stop it or re-run the installer with `--http-port=`/`--https-port=`. The installer detects this *before* changing anything and never kills an unrelated process for you. |
| **Database unavailable** (`/api/health/ready` returns 503) | `sudo systemctl status postgresql`; check `ConnectionStrings__DefaultConnection` in `/etc/silvertask/silvertask.env` matches the actual role/database/password; check `sudo -u postgres psql -l`. |
| **Migration failure** | Check `journalctl -u silvertask` and `/var/log/silver-task-install.log`. The app is never started against a partially-migrated database — fix the underlying issue (often a connectivity/permissions problem) and re-run `dotnet ef database update` from `/opt/silver-task/source`. |
| **Service won't start** | `sudo journalctl -u silvertask -n 100 --no-pager`. Common causes: `.env` missing/unreadable by the `silvertask` user, port 5000 already bound locally, or a build that didn't actually complete. |
| **Permission denied** (file storage) | `sudo chown -R silvertask:silvertask /var/lib/silver-task/attachments && sudo chmod 750 /var/lib/silver-task/attachments`. |
| **File storage unavailable** | Confirm the directory in `Attachments__StorageRoot` exists and is writable by the `silvertask` user; check available disk space (`df -h`). |
| **Email not sending** | Admin → Email → Send Test Email for a safe diagnostic; check `Smtp__*` values in `/etc/silvertask/silvertask.env`; check the Admin → Email delivery log for the specific failure classification (never raw credentials). |
| **HTTPS certificate failure** | Usually DNS: `dig +short your-domain` must already resolve to this server's public IP *before* certbot can issue a certificate. Fix DNS, then `sudo certbot --nginx -d your-domain`. |
| **DNS not configured** | The installer will still complete (HTTP-only) if certbot fails — point your domain's A/AAAA record at the server, then re-run certbot manually. |
| **Insufficient disk space** | Free up space (old kernels, logs, apt cache: `sudo apt-get clean`) or attach more storage — the installer's own pre-flight check catches this before installing, but ongoing growth (attachments, backups, Postgres WAL) needs its own monitoring. |

**Idempotency note**: running `install-debian.sh` again on an already-installed system does not
destroy your database, files, or `.env` — it detects the existing installation, warns you, and (in
non-interactive mode) safely re-applies configuration/rebuilds without touching data. Use
[Updating](#updating) for routine version upgrades instead.

## Security

Production recommendations, all either already enforced by the installer or worth confirming
manually:

- **Use HTTPS** — the installer configures this automatically when a domain is given (Let's
  Encrypt via certbot); `nginx` terminates TLS, and `ForwardedHeadersMiddleware` (added in Phase
  48 after being found missing — see [Release Information](#release-information)) ensures the app
  itself correctly understands requests arrived over HTTPS.
- **Strong, unique secrets** — the installer generates the database password and JWT signing key
  via `openssl rand -hex`, never hardcodes or reuses a development value, and never logs them.
- **Protect `.env`** — `/etc/silvertask/silvertask.env` is created with mode `640`, owned
  `root:silvertask`; never commit a filled-in copy (`.gitignore` blocks `.env`/`.env.*`, only
  `.env.example` is tracked).
- **Keep Debian and dependencies updated** — `sudo apt-get update && sudo apt-get upgrade`
  regularly; watch for .NET/Node/PostgreSQL/nginx security advisories.
- **Database is never exposed publicly** — the installer's firewall rules only open the ports
  nginx actually needs (80/443 by default); PostgreSQL (5432) is never opened, and Kestrel itself
  only listens on loopback (127.0.0.1) — nginx is the only public-facing process.
- **Firewall** — `ufw`, configured to allow only OpenSSH plus the HTTP/HTTPS ports actually in use;
  skip with `--skip-firewall` only if you're managing firewall rules another way.
- **Regular backups** — see [Backup](#backup); a backup you've never tested restoring isn't a real
  backup (see [Restore](#restore) for the procedure verified in Phase 47).
- **Authorization discipline** — every resource-scoped service re-derives access from the
  authenticated caller (never trusts a client-supplied ID alone); this was audited end-to-end in
  Phase 47 (IDOR probes, cross-project access attempts, unauthenticated/non-admin probes against
  admin routes) with **no Critical, High, or Medium findings**. See
  [Release Information](#release-information) for the full audit history.

### Security hardening (Phase 59)

> Inferred scope — see [docs/security-checklist.md](docs/security-checklist.md) for the full
> design rationale and administrator checklist covering authentication, authorization, secrets,
> configuration, deployment, input validation, headers, audit events, and dependency review.

Built on top of the Phase 47 audit above, closing gaps found by a renewed audit against the full
Phases 1–58 implementation (not just the original v1.0.0 code):

- **Security response headers** on every response (`SecurityHeadersMiddleware`) —
  `Content-Security-Policy: default-src 'self'` (verified safe for this SPA: no inline scripts/
  styles, no CSS-in-JS, no external CDNs, same-origin SignalR), `X-Content-Type-Options: nosniff`,
  `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, and `Strict-
  Transport-Security` (`UseHsts()`, Production only).
- **Login rate limiting** — `POST /api/auth/login` is limited per client IP (`Security__LoginRateLimit__PermitLimit`,
  default 10/60s), on top of the existing per-account lockout — the lockout stops repeated guesses
  against one account, this stops spraying many accounts from one source.
- **Admin-action audit logging** — role changes, activation/deactivation, password resets, and
  user deletion now log a structured `ILogger` line (`Admin action: ...`), the same mechanism this
  app already documents as its audit trail (ships to wherever you send `journalctl -u silvertask`).
- **systemd sandboxing** — `deploy/silvertask.service` now includes standard hardening directives
  (`ProtectSystem=strict`, `NoNewPrivileges`, `PrivateTmp`, etc.) on top of already running as a
  non-root user. **Existing installations**: `--activate` never re-copies the systemd unit, so this
  does not apply automatically on upgrade — see the checklist for how to apply it manually.
- **`scripts/update-debian.sh --security-check`** — a new read-only PASS/WARN/FAIL mode (mirrors
  `--doctor`) checking file permissions, firewall state, PostgreSQL exposure, systemd hardening,
  and response headers.
- **`scripts/check-dependencies.sh`** — wraps `dotnet list package --vulnerable`/`npm audit`
  (dev/CI use, alongside `certify-release.sh`, not run on every production deploy).
- **`scripts/security-probe.sh`** — a repeatable, live version of Phase 47's one-time manual audit:
  unauthenticated access, cross-role access, and an IDOR probe against a running instance.

## Uninstallation

```bash
sudo ./scripts/uninstall-debian.sh                    # stop/remove services, KEEP all data
sudo ./scripts/uninstall-debian.sh --remove-data       # also permanently delete DB/files/backups/.env
```

**By default, uninstalling only removes the service** — it stops and disables the systemd unit,
removes the unit file and nginx site config, and removes the application build/source directory.
It **never** deletes the database, uploaded files, backups, or `/etc/silvertask/silvertask.env`
unless you explicitly pass `--remove-data`, which then requires typing an exact confirmation phrase
(`DELETE`) before proceeding — there is no bare `-y`/Enter shortcut for permanent data loss. For
scripted teardown of a genuinely disposable test/staging environment only, `--remove-data --force`
skips the prompt (after a 5-second countdown you can still Ctrl+C).

PostgreSQL and nginx themselves are left installed (other applications on the host may use them) —
only Silver Task's own service, configuration, and (if requested) data are removed.

## Project Structure

```text
Silver-Task/
├── README.md
├── DEPLOYMENT.md              Manual/non-Debian production deployment reference
├── RELEASE_NOTES.md
├── .gitignore
├── .env.example
├── Silver-Task.slnx           Visual Studio solution
│
├── scripts/                   Debian installer suite (this document's install/update/backup/uninstall)
│   ├── install-debian.sh
│   ├── update-debian.sh
│   ├── backup-debian.sh
│   ├── uninstall-debian.sh
│   ├── certify-release.sh     Automated install/upgrade/rollback lifecycle testing (disposable hosts only)
│   ├── check-dependencies.sh  Dependency vulnerability scan (dotnet/npm), dev/CI use
│   ├── security-probe.sh      Live unauthenticated/unauthorized/IDOR probe against a running instance
│   └── lib/common.sh          Shared helpers (logging, checks, secret generation)
│
├── deploy/                    Reference config templates the installer generates from
│   ├── silvertask.service     systemd unit
│   ├── nginx.conf             reverse proxy config
│   ├── silvertask-logrotate   logrotate config (install/upgrade logs)
│   └── silvertask.env.example
│
├── Silver-Task.Server/        ASP.NET Core Web API
│   ├── Controllers/           HTTP endpoints (thin; no business logic)
│   ├── Services/               Business logic — one interface + implementation per file
│   ├── Models/Entities/        EF Core entities + enums
│   ├── Models/DTOs/            Request/response shapes (entities never round-trip to the client)
│   ├── Data/                   AppDbContext, Fluent API configurations, EF Core migrations
│   ├── Middleware/              Cross-cutting concerns (exception handling, etc.)
│   ├── Common/                  Shared helpers (claims extensions, domain exceptions, templates)
│   ├── Program.cs               App startup, DI, middleware pipeline
│   └── appsettings*.json        Non-secret configuration
│
└── silver-task.client/         React + TypeScript SPA
    └── src/
        ├── api/                 Centralized API client (fetch wrapper + per-resource services)
        ├── components/          Layout, auth guards, spreadsheet grid + cell editors, admin UI
        ├── hooks/                TanStack Query hooks
        ├── pages/                Route-level views
        ├── providers/            App-wide providers
        ├── routes/                React Router route definitions
        └── types/                 Shared TypeScript types
```

## Development

- **Linting**: client uses **oxlint**, not ESLint (`silver-task.client/.oxlintrc.json`).
  `react/rules-of-hooks` is an error; `react/only-export-components` is a warning fixed by moving
  shared constants out of component files rather than suppressing it.
- **SPA proxy**: in development the frontend and backend run as separate processes —
  `Silver-Task.Server.csproj` auto-launches Vite (`SpaProxyLaunchCommand`) and
  `silver-task.client/vite.config.js` proxies `/api/*` to the backend. Adding a new controller
  needs no proxy changes (it's a catch-all on `/api`).
- **Commands**:
  ```bash
  dotnet build                # backend
  npm run build                # frontend production build (also run as part of dotnet publish)
  npm run typecheck            # tsc -b --noEmit
  npm run lint                 # oxlint
  ```
- Working conventions (phase-gated development, core-stack constraints, etc.) are documented in
  `CLAUDE.md` for anyone continuing development with an AI pair-programmer.

## Testing

Not yet applicable — no automated test project exists in either the server or client as of v1.0.1.
This is the single biggest open gap noted throughout Phases 45–49 (see
[Known limitations](#known-limitations)) and is expected to be its own dedicated phase rather than
folded into a feature or stabilization phase. In its absence, every phase since 45 has been
verified through real `dotnet build`/`npm run build`/`typecheck`/`lint` runs plus live manual
testing against a running instance and database (documented per-phase in the
[Appendix](#appendix-feature--phase-implementation-notes)) — real verification, just not automated
or regression-proof yet.

## Release Information

**Current version: 1.0.1.** See `RELEASE_NOTES.md` for what changed in each release (v1.0.0 and
v1.0.1 to date).

### Known limitations

- **No automated test suite** — see [Testing](#testing).
- **No horizontal scaling** — background workers run in-process with no distributed locking; run
  exactly one application instance (see [Background Workers](#background-workers)).
- **Performance untested at real scale** — verified against a modest dataset; several report/list
  endpoints aggregate in memory rather than in SQL. Expected to be fine for a modest launch; see
  the Phase 47 notes in the Appendix for specific candidates if this becomes a problem.
- **No mobile-specific UI**, **no external integrations** (calendar sync, Slack/Teams, SSO,
  public API/webhooks), **no dedicated APM/metrics integration** — health endpoints
  (`/api/health`, `/api/health/ready`) and structured logs are the extent of built-in
  observability.
- **~910KB single-chunk frontend bundle** (234KB gzipped) — a route-level code-splitting candidate
  for a future release, not attempted yet.

### Development phases

This project was built incrementally across 49 phases plus the documentation/installer and
upgrade-engine phases that followed. Phases 1–13 have detailed prose write-ups (see the
[Appendix](#appendix-feature--phase-implementation-notes)); phases 14–44 are listed by title only
(the prose-per-phase style wasn't kept up); phases 45–49 have full write-ups in the Appendix and,
for 45–47, their own dedicated sections there.

- [x] **Phase 1** — Project architecture (TypeScript conversion, API client/routing skeleton, health endpoint).
- [x] **Phase 2** — PostgreSQL database model and EF Core migrations (all core tables/relationships/indexes).
- [x] **Phase 3** — Authentication & users (cookie-based JWT, secure-by-default authorization, first-user-admin bootstrap).
- [x] **Phase 4** — Projects & project members.
- [x] **Phase 5** — Tasks REST API.
- [x] **Phase 6** — Spreadsheet UI (TanStack Table grid, view-tab architecture).
- [x] **Phase 7** — Inline editing (optimistic updates + rollback).
- [x] **Phase 8** — Dropdown columns (Status/Priority/Assigned To).
- [x] **Phase 9** — Filtering, sorting & search.
- [x] **Phase 10** — Custom fields (EAV, 10+ types).
- [x] **Phase 11** — Task detail panel.
- [x] **Phase 12** — Comments & activity history.
- [x] **Phase 13** — Attachments (local-disk storage).
- [x] **Phase 14** — Application/admin scaffolding expansion.
- [x] **Phase 15** — Incremental feature work.
- [x] **Phase 16** — Task management refinements.
- [x] **Phase 17–20** — Project page feature build-out.
- [x] **Phase 21** — Incremental refinements.
- [x] **Phase 22** — My Tasks / Project page polish.
- [x] **Phase 23** — Project settings & user preferences groundwork.
- [x] **Phase 24** — Admin System Settings (generic key/value settings store).
- [x] **Phase 25** — Admin Custom Fields.
- [x] **Phase 26** — User Management and Delete User.
- [x] **Phase 27** — Security, permissions, and final review (`ProjectAccessService` authorization tiers).
- [x] **Phase 28** — Notifications (original in-app notification system).
- [x] **Phase 29** — Task Dependencies.
- [x] **Phase 30** — Subtasks.
- [x] **Phase 31** — Recurring Tasks.
- [x] **Phase 32** — Advanced Permissions (per-project roles).
- [x] **Phase 33** — File and Attachment Management (generalized).
- [x] **Phase 34** — File Organization.
- [x] **Phase 35** — Advanced Task Automation.
- [x] **Phase 36** — Advanced Notifications (email-capable notifications, digest/quiet-hours groundwork).
- [x] **Phase 37** — Advanced Dashboard and Personal Workspace.
- [x] **Phase 38** — Advanced Reporting and Analytics.
- [x] **Phase 39** — Advanced Task Dependencies and Workflow Automation.
- [x] **Phase 40** — Advanced Task and Project Templates.
- [x] **Phase 41** — Advanced Custom Fields and Dynamic Forms.
- [x] **Phase 42** — Advanced Search and Global Search.
- [x] **Phase 43** — Saved Views and Advanced Filters.
- [x] **Phase 44** — Notifications & Notification Center (real-time push via SignalR).
- [x] **Phase 45** — Email Notifications and Templates.
- [x] **Phase 46** — Scheduled Notifications and Digests.
- [x] **Phase 47** — Final V1 QA, Security Hardening & Release Preparation.
- [x] **Phase 48** — Production Deployment Prep (found/fixed two real production-only bugs — see Appendix). Version 1.0.0 released.
- [x] **Phase 49** — Post-Release Stabilization. Version 1.0.1 released.
- [x] **Phase 50** — README & Debian Installation System (this document, `scripts/`, `deploy/`).
- [x] **Phase 51** — Versioning Foundation. A single authoritative version (root `VERSION` file),
  consistency checks between it and each project's own version declaration, runtime version
  reporting via `GET /api/health`, an on-disk `installed-version.json` record, and
  `scripts/check-version.sh` for git-tag compatibility — see `DEPLOYMENT.md` → "Version
  information." Foundation only: no automatic upgrade system, one-click upgrade, or rollback yet.
- [x] **Phase 52** — Upgrade Engine Foundation. A CLI upgrade engine built into
  `scripts/update-debian.sh` (`--check`/`--status`/`--latest`/`--target-version`/`--dry-run`) that
  discovers stable git-tag releases, validates version/downgrade/metadata rules, and stages a
  target release into an isolated `git worktree` — see [Upgrade Engine](#upgrade-engine). Prepares
  and validates only: no activation, no web UI, no database rollback yet; the existing plain
  `update-debian.sh` invocation remains the only thing that actually deploys a change.
- [x] **Phase 53** — Upgrade Safety, Backups & Migration Orchestration. Extended
  `scripts/backup-debian.sh` with a crash-safe manifest, real structural backup verification
  (`pg_restore --list`, required-key checks), and safer retention; extended the upgrade engine with
  a persistent-data placement check and migration discovery/validation/planning via the project's
  own EF Core CLI (never executing one). A successful `--latest`/`--target-version` run now creates
  and verifies a pre-upgrade backup before ending in `READY_FOR_ACTIVATION` — see [Upgrade
  Engine](#upgrade-engine), [docs/upgrade-safety.md](docs/upgrade-safety.md), and
  [docs/restore.md](docs/restore.md). Still no activation, web UI, or automatic rollback.
- [x] **Phase 54** — Controlled Upgrade Activation & Health Validation. `sudo
  ./scripts/update-debian.sh --activate` — the first command that actually changes anything: builds
  the target release into a fresh directory, enables a new maintenance-mode middleware, swaps in
  the release (previous kept, never deleted), runs the real `dotnet ef database update`, restarts
  the service, validates health/version/smoke tests, and only then commits the installed version
  and disables maintenance mode. See [Upgrade Engine](#upgrade-engine) →
  "Activation," [docs/upgrade-activation.md](docs/upgrade-activation.md), and
  [docs/upgrade-operator-checklist.md](docs/upgrade-operator-checklist.md). Automatic rollback and a
  web UI are still not implemented — recovery from a failed activation remains a manual procedure.
- [x] **Phase 55** — Upgrade Recovery & Rollback System. `sudo ./scripts/update-debian.sh
  --rollback` — reactivates the release Phase 54's `--activate` preserved at
  `<publish-dir>.previous`, restoring the pre-upgrade database backup too (only if that upgrade
  actually required a migration — decided from the same recorded flag, never guessed), after first
  taking its own emergency backup of the current, failed database state. Same maintenance-mode/
  health/version-validation discipline as activation; installed version committed only after
  validation passes. See [Upgrade Engine](#upgrade-engine) → "Rollback,"
  [docs/rollback.md](docs/rollback.md), and
  [docs/rollback-operator-checklist.md](docs/rollback-operator-checklist.md). Covers only the
  single most recent activation (no multi-generation history); a web UI is still not implemented.
- [x] **Phase 56** — Upgrade Management, Release Channels & Production Hardening *(inferred scope —
  the original spec never arrived intact; see [docs/release-management.md](docs/release-management.md)
  for the full explanation)*. An opt-in `beta` release channel (`--channel`/`Upgrade__Channel`,
  `stable` remains the unconditional default), an opt-in maintenance-window policy
  (`Upgrade__MaintenanceWindow`, `--override-maintenance-window`), a durable append-only release
  history (`--history`), a read-only preflight command (`--doctor`), and log rotation for the
  install/upgrade logs (`deploy/silvertask-logrotate`). See [Upgrade Engine](#upgrade-engine) →
  "Release channels, maintenance window & preflight." No change to any Phase 51–55 default
  behavior; a web UI is still not implemented.
- [x] **Phase 57** — Automated Upgrade Testing & Release Certification *(inferred scope — the
  original spec never arrived intact; see [docs/release-certification.md](docs/release-certification.md)
  for the full explanation)*. `scripts/certify-release.sh` — a separate top-level script, gated
  behind an explicit `--disposable-host-confirmed` flag plus typed confirmation — orchestrates a
  full install → upgrade → validate → rollback → validate lifecycle test against a disposable host
  using the existing install/update/uninstall scripts unmodified, producing a durable
  `CERTIFIED`/`NOT_CERTIFIED` JSON Lines report. Report-only: certification never automatically
  gates anything in the runtime upgrade engine. See [Upgrade Engine](#upgrade-engine) →
  "Automated upgrade testing & release certification." No change to any Phase 51–56 default
  behavior; `update-debian.sh` itself is untouched by this phase.
- [x] **Phase 58** — Production Monitoring, Diagnostics & Operational Health *(inferred scope — the
  original spec never arrived intact; see [docs/monitoring-runbook.md](docs/monitoring-runbook.md)
  for the full explanation)*. A new Administrator-only `GET /api/admin/diagnostics` reports a
  single `healthy`/`degraded`/`failing` verdict (database reachability + latency, attachment
  storage disk space, and a heartbeat from each of the 6 interval-driven background workers) —
  the existing anonymous `GET /api/health`/`GET /api/health/ready` are untouched, still used by
  external uptime monitors. `scripts/update-debian.sh --doctor` gained one more read-only check
  against the existing health endpoint. No new required configuration — three optional
  `Diagnostics__*` thresholds all have defaults, so existing installations remain compatible with
  zero configuration changes.
- [x] **Phase 59** — Security Hardening & Production Security Validation *(inferred scope — the
  original spec never arrived intact; see [docs/security-checklist.md](docs/security-checklist.md)
  for the full explanation)*. A renewed audit against Phases 1–58 (not just the original Phase 47
  code) found and fixed: no security response headers anywhere (now
  `SecurityHeadersMiddleware` — CSP/`X-Content-Type-Options`/`X-Frame-Options`/`Referrer-Policy`/
  HSTS), no rate limiting on login (now IP-partitioned, on top of the existing per-account
  lockout), no admin-action audit logging (`UserService` role/activation/password-reset/deletion
  now logged), and zero systemd sandboxing in `deploy/silvertask.service` (now hardened — existing
  installs must apply it manually, since `--activate` never re-copies the unit). Added
  `--security-check` (mirrors `--doctor`), `check-dependencies.sh`, and `security-probe.sh` (a
  scripted, repeatable version of Phase 47's one-time manual audit). Also corrected a stale
  `RELEASE_NOTES.md` claim about an already-fixed finding. No new *required* configuration.

### GitHub / secrets hygiene

- Never commit `.env`, real connection strings, credentials, private keys, generated certificates,
  backup files, or installation logs — `.gitignore` blocks `.env*` (except `.env.example`),
  `appsettings.*.local.json`, build output, and `Silver-Task.Server/App_Data/` (local dev
  attachment storage).
- The Debian installer never writes secrets to `/var/log/silver-task-install.log` — only that a
  secret was generated/reused, never its value.
- Backup files (`scripts/backup-debian.sh`'s output) contain real credentials (the environment file
  copy) and database contents — they live under `/var/backups/silver-task` with root-only
  permissions and are never something this repository's `.gitignore` needs to worry about, since
  they're never created inside the repository tree.

---

## Appendix: feature & phase implementation notes

The sections below are the original, detailed per-feature/per-phase design documentation —
preserved in full rather than trimmed, since it explains the *why* behind decisions still load-bearing
today (authorization tiers, EAV custom fields, the SPA proxy setup, etc.). Sections earlier in this
README are the "how do I install/configure/operate this" material meant for a first read; this
appendix is the deeper "why does it work this way" reference.

### Database schema

Entity Framework Core (Npgsql provider) maps the following core tables (of 51 total as of v1.0.1),
configured via `IEntityTypeConfiguration<T>` classes in `Data/Configurations/`:

| Table | Purpose |
|---|---|
| `Users` | Accounts. Global `Role` (Administrator/Manager/Member), hashed passwords, `IsActive` for soft deactivation. |
| `Projects` | Owned by a `User`; `IsArchived`/`ArchivedAt` for archiving instead of hard delete. |
| `ProjectMembers` | Join table granting a user access to a project (unique per project+user). |
| `Tasks` | The spreadsheet rows. Status/Priority are fixed enums stored as text; `SortOrder` is a fractional index for drag-reordering without renumbering siblings. |
| `TaskComments` | Threaded comments on a task. |
| `TaskActivities` | Append-only audit log (`Action`, `FieldName`, `OldValue`, `NewValue`) — survives the acting user being deleted. |
| `TaskAttachments`/`Attachments` | File metadata (name, size, MIME type, storage path) — see [File Storage](#file-storage). |
| `CustomFields` / `CustomFieldOptions` / `TaskCustomValues` | EAV-style value storage: one row per (task, custom field), so adding a custom field never requires a schema migration. |

All primary keys are `uuid` (`Guid`), which lets the client generate an id for a new row and render
it optimistically before the API confirms it. The task entity/table is named `TaskItem`/`Tasks`
(not `Task`) to avoid colliding with `System.Threading.Tasks.Task`, in scope everywhere via
`ImplicitUsings`.

### Projects & authorization model

- Anyone authenticated can create a project; the creator becomes its `Owner` and is automatically
  added as a `ProjectMember`.
- **View access**: Administrators, the project owner, or any project member.
- **Manage access** (rename, add/remove members, archive, delete tasks): Administrators, the
  project owner, or a `Manager` who is a member of that specific project — enforced in
  `ProjectAccessService` (`EnsureCanParticipateAsync`/`EnsureCanEditAsync`/`EnsureCanManageAsync`),
  shared by every resource-scoped service so authorization can't drift out of sync between them.
- The owner can never be removed via the members endpoint (409 Conflict) — ownership transfer isn't
  implemented.
- `DELETE /api/projects/{id}` archives rather than deletes the row.
- Members are added **by email**, not by browsing a user directory (`GET /api/users` is
  Administrator-only).

### Tasks

- `PUT /api/tasks/{id}` is a full-resource replace, matching the pattern used for Projects/Users.
- Viewing/creating/editing tasks requires the participate tier (any project member); **deleting**
  requires the manage tier.
- Assigning a task validates `assignedToUserId` is an actual project member (400 if not).
- `CompletedAt` is managed automatically on Status transitions to/from `Complete`.
- `SortOrder` is a fractional `double` index — new tasks append at `max + 1`; duplicates insert at
  the midpoint after the original, so drag-reorder never needs to renumber siblings.
- Deleting a task hard-deletes; comments/activity/attachments/custom values cascade with it.

### Spreadsheet UI

- TanStack Table: sticky header, horizontal scroll, drag-to-resize columns.
- **`TaskTable` deliberately uses `useLegacyTable`** from `@tanstack/react-table/legacy` — the
  installed v9 replaced `useReactTable` with a new modular `features`-based API, but the
  officially-bundled v8-compatible legacy layer still covers everything this grid needs (including
  column resizing) and is simpler/lower-risk for a plain display grid. Revisit only if a future
  feature needs something v9-only.
- `ProjectViewTabs` renders all five views (Table/Kanban/Calendar/Timeline/Gantt) — see
  [Features](#features).

### Inline editing

Click-to-edit cells (`EditableTitleCell`/`EditableDateCell`) with `Enter`-commits/
`Escape`-cancels/`Tab`-moves-on. **Optimistic update + rollback**: `useUpdateTask` patches the
TanStack Query cache immediately in `onMutate`, snapshots prior state, and restores it in `onError`
on failure — a failed edit leaves the cell with a red outline until the next attempt. Since
`PUT /api/tasks/{id}` is a full-resource replace, `buildBaseRequest` fills in unchanged fields for
every single-field edit.

### Dropdown columns

Status/Priority are always-rendered native `<select>` elements styled as colored badges — no
click-to-open-then-select two-step, since choosing an option is inherently a single atomic action.
Assigned To is populated from **the project's members only**, with an explicit "Unassigned" option.
The backend still enforces "assignee must be a project member" server-side even though the dropdown
only offers valid members (two tabs open, stale membership state, etc.).

### Filtering, sorting & search

**Fully client-side** (`useTaskFilters.ts`) — the task list has no pagination yet, so there's
nothing to gain from round-tripping to the server per keystroke; this is explicitly deferred to a
future performance/virtualization phase once real scale requires it. Search matches title,
description, or Text/LongText custom field values. Filters (Status/Priority/Assigned
To/Due-before) are AND-combined. Sort covers Task/Assigned To/Status/Priority/Due Date/Created
Date/Updated Date via both a toolbar menu and clickable column headers driving the same state.
Status/Priority sort by severity rank, not alphabetically.

### Custom fields

- Managing field *definitions* uses the manage tier; *setting a value* uses the participate tier
  (any project member).
- **`FieldType` is immutable after creation** — changing Number to Date after values exist would
  leave them uninterpretable.
- Every value is stored as text and validated/normalized per `FieldType`
  (`TaskService.ValidateAndNormalizeCustomValueAsync`): Number/Currency parse as `decimal`,
  Date/DateTime parse, Checkbox is `"true"`/`"false"`, Dropdown/MultiSelect store option **ids**
  (so renaming an option doesn't orphan values), User must be a project member, Link is JSON
  `{"label","url"}` restricted to `http`/`https` (closing off a `javascript:` XSS vector).
- Deleting an option cleans up any `TaskCustomValues` referencing it rather than leaving them
  dangling.
- Frontend cell editors (`components/spreadsheet/*CustomValueCell.tsx`) reuse the interaction
  patterns already established per type (click-to-edit text, always-rendered `<select>`, a
  `<details>` checklist popover for MultiSelect, a two-field popover for Link) rather than
  inventing new ones.

### Task detail panel

Opened via a dedicated expand icon (not by clicking a cell, which already does inline editing) —
the same pattern Airtable/Notion/Linear use for this exact conflict. Driven by the `?task=<id>`
query parameter (not local component state), making it linkable and back-button-closeable. Reuses
the grid's own dropdown/date/custom-field cell components unmodified inside a form layout — none of
them were ever coupled to being inside a `<td>`.

### Comments & activity history

- **Comments are author-only** for edit/delete — no manage-tier or Administrator override, a
  literal reading of the original spec, verified directly (even an Administrator gets 403 editing
  someone else's comment).
- **Activity history is built by diffing old vs. new values inline** inside `TaskService`
  (`CreateAsync`/`UpdateAsync`/`DuplicateAsync`/`SetCustomValueAsync`), not a generic snapshot
  mechanism. `SortOrder` changes are deliberately excluded from the diff.
- Assignment gets its own `"Assigned"` action distinct from generic `"FieldChanged"`, for natural
  phrasing ("X assigned this task to Y").

### Attachments

- **Storage is local disk**, deliberately not "complicated object storage" — see
  [File Storage](#file-storage) for the current operational details. Swapping to a cloud-backed
  implementation later is a contained change behind `IAttachmentService`.
- 25MB size cap, small blocklist of dangerous executable extensions
  (`.exe`/`.dll`/`.bat`/`.cmd`/`.sh`/`.ps1`/`.msi`/`.com`/`.scr`).
- Delete authorization sits between comments' "author only" and tasks' "manage tier": the uploader
  can always remove their own upload; Administrators/owners/manager-members can remove any
  attachment on the project.
- Upload/delete are logged into the activity feed (`"AttachmentAdded"`/`"AttachmentRemoved"`).

### Email notifications (Phase 45)

Extends the email-capable notification system already built in Phase 36 (`IEmailService`,
`NotificationTemplates`, per-(user, notification-type) toggles, digest emails, quiet hours) rather
than replacing it:

- **Background delivery queue** — `NotificationService.MaybeSendEmailAsync` enqueues a
  self-contained `EmailDelivery` row instead of sending inline, so a slow/unreachable SMTP server
  can never block a task/comment/project write. `EmailDeliveryBackgroundService` polls every 20
  seconds, re-validates the recipient's access and the task/project's continued existence
  immediately before sending, and retries twice with backoff (2 min, then 10 min) before marking a
  row `Failed`. `EmailDelivery.LastError` only ever stores a short, generic classification, never
  raw exception text/credentials.
- **User-level master email switch** (`UserPreference.EmailNotificationsEnabled`) checked before
  any per-type toggle.
- **Admin email configuration** — SMTP status (never credentials), Send Test Email, a read-only
  delivery log (status/type/timestamps only).
- **Customizable templates** — controlled `{{Variable}}` substitution only (`{{UserName}}`,
  `{{ActorName}}`, `{{TaskName}}`, `{{ProjectName}}`, `{{DueDate}}`, `{{ActionUrl}}`), never
  arbitrary code/markup; the final composed string is HTML-encoded as a whole.

### Notification digests (Phase 46)

Replaces the Phase 36 global "Daily digest" switch with per-notification-type delivery modes and
real Daily/Weekly digest scheduling:

- **Delivery modes** — `UserNotificationSetting.EmailDeliveryMode` (`Immediately`/`DailyDigest`/
  `WeeklyDigest`/`Off`), one value per notification type. Urgent-priority types (currently only
  `TaskOverdue`) always send immediately regardless of the stored mode.
- **Schedule** — per-user `DailyDigestTime`/`WeeklyDigestDay`/`WeeklyDigestTime`, interpreted in
  the user's own timezone. The scheduler ticks every 10 minutes with no upper time-window bound, so
  a missed run (app was offline) still catches up the same day once the app resumes.
- **Content** — built entirely from existing `Notification` records and live `Tasks`/`Projects`
  queries, re-checked against current project access so items from since-revoked access are
  silently excluded.
- **Delivery** — digest HTML is rendered once and enqueued through the *same* Phase 45 queue/retry
  — never a second retry loop.
- Known limitation: a notification type with `InAppEnabled = false` never produces a `Notification`
  row, so it can never appear in a digest even if its email mode is Daily/Weekly (digest content is
  deliberately sourced only from existing records, not a parallel event log).

### V1.0.0 release readiness (Phase 47)

Three parallel read-only code audits (security/authorization, performance/database,
migration/data-integrity) plus a live pass against the running dev server and database (smoke test,
IDOR/auth probes, an actual `pg_dump`/restore, and a from-empty migration run).

**Fixed**: an N+1 role lookup in project search; an `AutomationService` query that materialized
every automation in the system into memory before filtering (an untranslatable LINQ predicate);
added `GET /api/health/ready`.

**Security audit result**: no Critical, High, or Medium findings. One Low finding (a hardcoded
demo-seed password) already correctly gated to `--seed` + `Environment.IsDevelopment()`.

**Migration audit result**: PASS — all migrations apply cleanly to a brand-new empty database,
verified by actually running `dotnet ef database update` against a fresh scratch database.

**Backup & restore**: performed for real — a dump of the dev database, restored into a separate
scratch database, with row counts, `__EFMigrationsHistory` count, and foreign-key constraint count
all confirmed to match exactly before the scratch database was dropped. See [Backup](#backup) /
[Restore](#restore) for the current operational procedure.

### Production deployment prep (Phase 48) and post-release stabilization (Phase 49)

Phase 48 built and ran the actual `dotnet publish -c Release` artifact for the first time (Phases
45–47 had only ever exercised the dev server, via Vite's SpaProxy) and found two real
production-only bugs invisible in dev mode:

1. **Total lockout**: the SPA shell and static JS/CSS assets required authentication in a real
   published build — nobody could load even the login page. Fixed via `.AllowAnonymous()` on
   `MapStaticAssets()`/`MapFallbackToFile()`.
2. **Reverse-proxy correctness**: no `ForwardedHeadersMiddleware` was configured, which risks
   `UseHttpsRedirection()` behaving incorrectly behind a TLS-terminating proxy. Fixed.

Both were verified against the actual published binary. Version 1.0.0 released.

Phase 49 (a renewed code-level review, since v1.0.0 had not yet been deployed anywhere with real
production telemetry) found and fixed: raw exception messages leaking into automation
error fields visible to non-admin project Managers; a missing `TraceId` in exception logs breaking
incident correlation; a silent frontend failure on two admin template actions; a swallowed
bookkeeping exception. Version 1.0.1 released. See `RELEASE_NOTES.md` for the full list.
