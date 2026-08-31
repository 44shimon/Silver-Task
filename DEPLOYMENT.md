# Silver Task v1.0.0 — Production Deployment Runbook

This document is the Phase 48 deliverable: everything needed to deploy Silver Task to a real
production environment. It was prepared and verified against a locally-built **Release
configuration, published artifact** — the same artifact a real deployment would use — but this
repository has no actual production server, domain, or hosting account connected to it. Steps
below marked **[VERIFIED]** were actually run and confirmed during this phase; steps marked
**[REQUIRES YOUR INFRASTRUCTURE]** are written and ready but need a real target to execute against.

## What was actually verified in this phase

Running the real `dotnet publish -c Release` artifact (not the dev server) surfaced and fixed two
genuine production-only bugs that no amount of dev-mode testing (Phases 45–47) could have caught,
because dev mode serves the SPA through a separate Vite process that never exercises this code
path at all:

1. **Critical — total lockout**: `Program.cs`'s `MapStaticAssets()`/`MapFallbackToFile()` had no
   `AllowAnonymous()`, so the global `FallbackPolicy` (auth-required-by-default) applied to the
   SPA's own `index.html` and JS/CSS bundle. In a real deployment, **no one could ever load the
   app at all** — not even the login page — because loading the login page requires fetching the
   JS bundle that renders it, and that fetch would 401. Fixed: both now carry `.AllowAnonymous()`.
2. **High — reverse-proxy correctness**: no `ForwardedHeadersMiddleware` was configured. Behind a
   TLS-terminating reverse proxy (the deployment topology this app is built for), Kestrel would
   only ever see plain HTTP from the proxy, which risks `UseHttpsRedirection()` behaving
   incorrectly and any code reading `Request.Scheme` getting the wrong answer. Fixed: added,
   configurable via `ForwardedHeaders:KnownProxies` for proxies not on the same host as the app.

Both fixes were verified **[VERIFIED]** against the actual published Release binary, running with
production-style configuration (environment variables, not user-secrets) on this machine: SPA and
static assets load without authentication, the API still correctly requires it, login/session
round-trips work, `X-Forwarded-*` headers are accepted, and `dotnet publish` itself completes
without error (backend + the SPA build it triggers via the `ProjectReference`).

## Pre-deployment verification — [VERIFIED]

- Phase 47 final report: **READY FOR V1 RELEASE** (no open Critical/High issues).
- `dotnet build` (Debug) — clean, 0 warnings, 0 errors.
- `dotnet publish -c Release` — clean, produces a working self-contained `wwwroot` + server DLL.
- `npm run build` / `npm run typecheck` / `npm run lint` — clean.
- `dotnet ef migrations script --idempotent` generates without error from empty → current; a full
  from-empty `dotnet ef database update` against a scratch database was also run for real (Phase
  47) and produced a schema matching the existing dev database exactly.
- No secrets committed to git (re-confirmed: `appsettings*.json` have empty placeholders only,
  `.gitignore` covers `.env*`/`appsettings.*.local.json`, no `.env` tracked).
- A real backup was taken and a real restore was performed and verified row-for-row (Phase 47).

## Production environment review — [REQUIRES YOUR INFRASTRUCTURE]

This app has no infrastructure-as-code or existing hosting target in this repository — every item
below needs a real server/hosting account before it can be executed, not just documented:

| Component | What Silver-Task needs | Where to configure |
|---|---|---|
| Application server | Linux/Windows host with .NET 10 runtime (or a container), able to run the published `Silver-Task.Server.dll` as a persistent service | `deploy/silvertask.service` (systemd example) |
| Database server | PostgreSQL (version matching `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x compatibility — Postgres 14+ recommended) | `ConnectionStrings__DefaultConnection` |
| File storage | A writable directory for `Attachments:StorageRoot` (default `App_Data/attachments`), sized for expected upload volume, backed up on the same cadence as the database | app config / `appsettings.json` |
| Background workers | No separate process — the 7 `BackgroundService`s (email delivery, digests, due-date sweep, recurring tasks, automations, notification retention) run **inside** the same web process; do not deploy a second copy expecting a separate worker role | N/A — confirm only one instance of the app process is running (see "Background worker deployment" below) |
| Reverse proxy | nginx/Caddy/IIS-ARR terminating TLS, forwarding to Kestrel on loopback, with WebSocket upgrade support for `/hubs/notifications` | `deploy/nginx.conf` (example) |
| HTTPS | A real certificate (Let's Encrypt via certbot, or your CA of choice) | reverse proxy config |
| Domain / DNS | A domain pointed at the server's public IP | your DNS provider — out of scope for this repo |
| Email | Real SMTP credentials (any provider — Microsoft 365, Google Workspace, SendGrid SMTP relay, etc.) | `Smtp__*` env vars — see `deploy/silvertask.env.example` |
| Monitoring | See "Monitoring" section below | your existing ops stack |
| Disk / memory / CPU | No unusual requirements — a small Postgres-backed CRUD app; size per expected user/task/attachment volume, monitor disk headroom for both the database and `Attachments:StorageRoot` | your infrastructure |

## Deployment steps

1. **[REQUIRES YOUR INFRASTRUCTURE] Backup first.** Before touching production, take a real
   backup (see README → "Backup & restore", the exact procedure verified in Phase 47):
   `pg_dump -h <host> -U <user> -d <database> -F c -f backup.dump`, plus a copy of the
   `Attachments:StorageRoot` directory. Confirm the backup file is non-empty and record its
   location per your existing operational procedure.
2. **[REQUIRES YOUR INFRASTRUCTURE] Provision environment variables.** Copy
   `deploy/silvertask.env.example` to the real environment file location, fill in real values
   (unique production `Jwt:Secret`, real DB credentials, real SMTP credentials if using email,
   real `Cors:AllowedOrigins`), `chmod 600` it, and never commit the filled-in version.
3. **[VERIFIED locally / REQUIRES YOUR INFRASTRUCTURE to run against the real DB] Publish and
   apply migrations**:
   ```bash
   dotnet publish Silver-Task.Server/Silver-Task.Server.csproj -c Release -o <publish-dir>
   dotnet ef database update --project Silver-Task.Server --startup-project Silver-Task.Server
   ```
   `dotnet ef database update` is idempotent — safe to run on every deploy, only applies
   migrations not already recorded in `__EFMigrationsHistory`. It never resets or recreates
   existing tables; it only adds what's missing.
4. **[REQUIRES YOUR INFRASTRUCTURE] Deploy the service.** Copy the publish output to the server,
   install `deploy/silvertask.service`, `systemctl daemon-reload && systemctl enable --now
   silvertask`. This gives you: survives terminal disconnect (systemd, not a foreground shell
   process), survives reboot (`enable`), and auto-restarts on crash (`Restart=always`).
5. **[REQUIRES YOUR INFRASTRUCTURE] Install the reverse proxy** using `deploy/nginx.conf` as a
   starting point, get a real TLS certificate, reload nginx.
6. **[REQUIRES YOUR INFRASTRUCTURE] Verify startup**: `curl https://<domain>/api/health` (liveness)
   and `curl https://<domain>/api/health/ready` (confirms DB connectivity) should both return
   `{"status":"ok",...}`. If `/ready` returns 503, the app is up but can't reach the database —
   check `ConnectionStrings__DefaultConnection` and network/firewall rules before anything else.
7. **[REQUIRES YOUR INFRASTRUCTURE] Run the smoke test** in the next section against the real
   deployed URL.

## Post-deployment smoke test checklist

Everything in this checklist was exercised **[VERIFIED]** against the local Release-configuration
build during this phase (proving the code path works); re-run the same steps against the real
deployed URL once it exists, since only that run counts as an actual production verification:

- [ ] Admin login → dashboard, user management, settings all load
- [ ] Standard user login → sees only their authorized projects/tasks, can edit permitted tasks,
      logout actually invalidates the session (confirmed: a request with the old cookie after
      logout returns 401)
- [ ] Create project → create task → assign → edit → change status → comment → upload file →
      complete task, with activity history and notifications generated at each step
- [ ] Table/Sheet, Kanban, Calendar, Timeline, Gantt views all load the same underlying task data
      correctly (they're client-rendered from one API response — verified that response is
      correct and complete)
- [ ] Global search, advanced filters, saved views all return results scoped to the caller's
      actual project access (re-verified in Phase 47's IDOR probes: a user requesting another
      user's/project's resource by substituted ID gets 403/404, never data)
- [ ] File upload/download works; unauthorized download attempt is blocked
- [ ] In-app notifications appear, read/unread state works, per-type preferences apply
- [ ] Test email sends successfully via Admin → Email → Send Test Email (uses real SMTP config,
      the safe built-in way to verify email delivery without spamming real users)
- [ ] Daily/Weekly digest generates correctly for a test account with a type set to Daily/Weekly
      digest mode (see README → "Notification digests" for exactly how this was verified against
      a real send-and-retry cycle in Phase 46)
- [ ] Automations trigger/condition/action pipeline fires once per event, no duplicate execution
- [ ] `GET /api/health` and `GET /api/health/ready` both return healthy

## Background worker deployment

Silver-Task's background processing is **in-process**, not a separate deployable — all 7
`BackgroundService`s (email delivery + retry, daily/weekly digest scheduling, due-date
notifications, recurring task generation, automation execution, automation overdue checks,
notification retention cleanup) start automatically the moment the web app itself starts, inside
the same process, registered in `Program.cs`. There is nothing extra to deploy or start for
background jobs to work — but this also means **running two copies of the app process
simultaneously runs two copies of every background worker**, and this codebase has no distributed
locking to prevent double-processing (documented as a known limitation in the Phase 46 README
section). Deploy exactly one instance, or add distributed coordination first if you need more than
one.

## Monitoring

No dedicated APM/metrics library is wired into this codebase (no Application Insights/OpenTelemetry
exporter, etc. — reusing "the monitoring approach established in the existing infrastructure" per
the phase brief, and no such infrastructure exists in this repo to reuse). What v1.0.0 actually
provides for a monitoring stack to hook into:

- `GET /api/health` / `GET /api/health/ready` (Phase 48, this document) — poll these for
  liveness/readiness from any external monitor (Uptime Robot, a Kubernetes/orchestrator probe, a
  simple cron + alert script). `/ready` failing means "app is up but the database is unreachable,"
  which is the single most useful automated signal this app can currently give you.
- Structured application logs via the standard ASP.NET Core `ILogger` — every background service
  logs failures per-tick (`_logger.LogError`) rather than crashing silently; ship stdout/stderr
  (or `journalctl -u silvertask` under the example systemd unit) to whatever log aggregation you
  already run.
- The Admin → Email delivery log (`/api/admin/email/deliveries`) is a built-in, admin-visible
  signal for email-specific failures without needing external tooling.
- Disk space for both PostgreSQL's own data directory and `Attachments:StorageRoot` should be
  monitored by your existing host-level tooling — this app doesn't self-report disk usage.

Building a dedicated metrics/APM integration is real, additional work — not attempted here per the
phase's "do not rebuild the application" instruction; the health endpoints above are the
appropriately-scoped v1.0.0 answer to "can the outside world tell if this is broken."

## Rollback procedure

1. **Stop the new version**: `sudo systemctl stop silvertask` (or equivalent).
2. **Application rollback**: redeploy the previous publish output (keep the last known-good
   publish directory/artifact around specifically for this) and restart the service. Since
   background workers are in-process (see above), stopping the service stops them too — no
   separate worker rollback needed.
3. **Database rollback** — two cases:
   - If the new version added migrations but they haven't caused a problem themselves (the bug is
     in application code, not the schema): just roll back the application binary (step 2); the
     newer schema is forward-compatible with nothing yet using it.
   - If a migration itself needs reverting: `dotnet ef database update <PreviousMigrationName>
     --project Silver-Task.Server --startup-project Silver-Task.Server` runs that migration's
     `Down()`. **Understand the data consequences first** — every `Down()` in this codebase was
     reviewed for correctness in Phase 47, but a `Down()` that drops a column drops any data
     users have since put in it. If real user data now depends on the new schema, prefer
     restoring from the pre-deployment backup (step 1 of "Deployment steps") over running `Down()`
     against live data.
4. **Full restore fallback**: if rollback via steps 2–3 isn't sufficient, restore the
   pre-deployment database backup and attachment storage backup per README → "Disaster recovery
   procedure," then redeploy the previous application version.
5. **Never** perform a destructive database operation (`Down()`, a restore, a manual `DROP`)
   without a verified-good backup already in hand — this is the same rule Phase 47 established
   for the original migration, restated here because it applies equally to a rollback.

## Version information

`silver-task.client/package.json` and `Silver-Task.Server/Silver-Task.Server.csproj` both declare
`1.0.0`. There's no separate runtime "About" page exposing version info to end users in this
release — out of scope to add for v1.0.0 (a small, real feature addition, not a deployment task).
