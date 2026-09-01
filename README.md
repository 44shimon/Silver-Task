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

Phase 52 adds a second, additive command surface to the same `scripts/update-debian.sh` — a CLI
upgrade engine that **discovers, validates, and stages** a target release without activating it.
This is deliberately **not** a replacement for the plain `sudo ./scripts/update-debian.sh` above:
only that command (and `--ref=`/`--skip-backup`) actually backs up, rebuilds, migrates, restarts,
and deploys a change today. The upgrade engine's job is **PREPARE**, not **ACTIVATE** — future
phases will teach a prepared release to hand off into an activation step; Phase 52 stops short of
that on purpose (no backup is taken, no migration runs, no service is restarted, and
`installed-version.json` is never modified by any of the commands below).

```bash
sudo ./scripts/update-debian.sh --check                        # is a newer stable release available?
sudo ./scripts/update-debian.sh --status                       # upgrade/version status report
sudo ./scripts/update-debian.sh --latest                       # validate + stage the latest stable release
sudo ./scripts/update-debian.sh --target-version 1.1.0         # validate + stage a specific stable release
sudo ./scripts/update-debian.sh --dry-run --latest              # show what --latest would do; changes nothing
sudo ./scripts/update-debian.sh --target-version 1.1.0 --yes   # skip the confirmation prompt (for automation)
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
skip with `--yes`), then fetch the target tag and stage it into an isolated `git worktree` under
`$SILVERTASK_INSTALL_DIR/upgrade-staging/<version>` — a second checkout that shares git objects
with the main install but never touches `$SILVERTASK_INSTALL_DIR/source` (what the plain `update-
debian.sh` above actually builds from) or the running application.

**`--dry-run`** (with `--latest`/`--target-version`) runs every validation step and reports the
same plan, but never fetches new git tags, never creates a worktree, never acquires the upgrade
lock, and never writes upgrade state — the only side effect is an informational log line.

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

**Upgrade lock**: only one upgrade preparation can run at a time, enforced with `flock` on
`/var/lock/silvertask-upgrade.lock` — an OS-held lock tied to the process, so a crashed/killed
attempt releases it automatically and can never permanently block a future one. `--status` reports
`IN PROGRESS` (with target/start time) while the lock is actually held, or `STALE UPGRADE LOCK
DETECTED` if a previous attempt's leftover state file shows it never finished cleanly but nothing
currently holds the lock — safe to just retry in that case; nothing was ever activated.

**Upgrade state**: `$SILVERTASK_INSTALL_DIR/upgrade-state.json` records the most recent prepare
attempt (current/target version, status, step, timestamps) for `--status` to report — left in place
after success, failure, or a crash, and only ever overwritten by the next real attempt, never
auto-deleted.

**Upgrade log**: `/var/log/silver-task/upgrade.log` — a structured, timestamped history of every
upgrade-engine invocation, separate from the installer's own `/var/log/silver-task-install.log`.
Never contains secrets/env vars/credentials, same discipline as every other log this project
writes.

**Exit codes**:

| Code | Meaning |
|---|---|
| 0 | Success / no blocking problem (including a deliberate no-op: already on target, or declined the confirmation prompt) |
| 1 | General error |
| 2 | Invalid arguments |
| 3 | Version inconsistency (installed ≠ running, or unknown) |
| 4 | Target version unavailable |
| 5 | Unsupported upgrade path (downgrade, or a release's `minimumSupportedVersion` not met) |
| 6 | Upgrade already in progress |
| 7 | Repository access failure |
| 8 | Insufficient disk space |

**Current limitations (Phase 52)**: this is a foundation, not the full upgrade system. There is no
web-based one-click upgrade UI, no automatic activation of a staged release, and no automatic
database rollback — activating a prepared release still means running the plain `sudo
./scripts/update-debian.sh` (optionally `--ref=v1.1.0`) documented above. Disk-space checking is an
approximation (precise backup-size estimation is future work). A future phase is expected to teach
the engine to hand a validated, staged release off into a real activation step.

## Backup

```bash
sudo ./scripts/backup-debian.sh                # backs up to /var/backups/silver-task, keeps last 7
sudo ./scripts/backup-debian.sh --keep=30       # keep the last 30 backup sets instead
```

Each run creates a timestamped set containing: a `pg_dump` (custom format) of the database, a
`tar.gz` of the file storage directory, and a copy of the environment file (permissions restricted
to root — it contains secrets, and the backup log never records their values). The script verifies
the database dump is non-empty before reporting success, and applies retention (deletes the oldest
sets beyond `--keep`) only after a successful backup.

Run this on a schedule via cron or a systemd timer, e.g. nightly:

```bash
echo "0 3 * * * root /opt/silver-task/source/scripts/backup-debian.sh >> /var/log/silver-task-backup.log 2>&1" | sudo tee /etc/cron.d/silver-task-backup
```

## Restore

**There is no automated restore script** — restoring is rarer and higher-stakes than backing up, so
it's deliberately a manual, reviewable set of commands rather than something a script runs
unattended:

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
│   └── lib/common.sh          Shared helpers (logging, checks, secret generation)
│
├── deploy/                    Reference config templates the installer generates from
│   ├── silvertask.service     systemd unit
│   ├── nginx.conf             reverse proxy config
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
versioning-foundation phases that followed. Phases 1–13 have detailed prose write-ups (see the
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
