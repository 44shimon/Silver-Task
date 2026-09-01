# Upgrade safety workflow (Phase 53)

This document explains what `sudo ./scripts/update-debian.sh --latest` / `--target-version X.Y.Z`
actually do, step by step, and why each step exists. For the command reference (flags, exit codes,
`--dry-run`), see README → [Upgrade Engine](../README.md#upgrade-engine). For restoring a backup,
see [restore.md](restore.md).

**This is a preparation workflow, not an activation one.** Every step below either reads existing
state or creates new, separate artifacts (a staged git worktree, a backup, a migration plan file).
Nothing replaces the running application, runs a database migration, or restarts a service. The
result of a successful run is `READY FOR ACTIVATION` — activation itself is a future phase.

## The workflow

```text
Upgrade Requested (--latest or --target-version X.Y.Z)
        │
        ▼
Validate Installation                  installed version known, matches what's actually running
        │
        ▼
Resolve & Validate Target              discover stable releases, reject downgrade/same-version,
        │                              validate release metadata (releases/<version>.json)
        ▼
Acquire Upgrade Lock                   flock — only one upgrade attempt at a time
        │
        ▼
Stage Release                          isolated `git worktree` — never touches the active install
        │
        ▼
Check Persistent Data Locations        attachments storage isn't inside anything an upgrade replaces
        │
        ▼
Create Database Backup                 pg_dump (custom format), via scripts/backup-debian.sh
        │
        ▼
Verify Database Backup                 pg_restore --list — structural read, not just "file exists"
        │
        ▼
Create Configuration Backup            copy of silvertask.env, permissions restricted
        │
        ▼
Verify Configuration Backup            required keys present (values never inspected/logged)
        │
        ▼
Validate Current Migration State       dotnet ef migrations list (real DB, current code) — the
        │                              currently-installed code's own migrations must be fully,
        │                              cleanly applied before planning anything on top of it
        ▼
Validate Target Migrations             dotnet ef migrations list --no-connect (staged worktree,
        │                              zero DB connection) — malformed/duplicate migrations block here
        ▼
Generate Migration Plan                dotnet ef migrations script --idempotent (zero DB connection)
        │                              — a real, inspectable SQL file; nothing is executed
        ▼
READY FOR ACTIVATION
```

Any failure at any step: the staged worktree is cleaned up, upgrade state is written as `FAILED`
with the step name, the failure is logged, and the command exits with a specific code (see README's
exit code table) — the running application, database, and installed version are left exactly as
they were before the attempt started.

## Persistent data inventory

What Silver Task actually persists, whether an upgrade touches it, and how each is protected:

| Location | Contents | Does an upgrade touch it? | Protection |
|---|---|---|---|
| PostgreSQL database (`ConnectionStrings__DefaultConnection`) | All application data — projects, tasks, users, comments, activity history, custom fields, settings | Only via an explicit, backed-up-first migration step (future activation phase) | Backed up + verified before every prepared upgrade; runs on its own server process, never inside the application's install/staging directories |
| `Attachments__StorageRoot` (default `/var/lib/silver-task/attachments`) | Uploaded file attachments | Never — the application only ever reads/writes here at runtime, never as part of staging/building a release | Backed up (tar.gz) before every prepared upgrade; `st_up_persistent_data_check` (`scripts/lib/upgrade.sh`) verifies it isn't nested inside `$SILVERTASK_INSTALL_DIR/source`, `.../publish`, or `.../upgrade-staging` — the exact "uploads inside the build directory" misconfiguration that would otherwise cause data loss on activation |
| `/etc/silvertask/silvertask.env` | Database credentials, JWT signing secret, SMTP credentials, CORS/base-URL config | Never — activation only ever reads it, never rewrites it | Backed up (permissions-restricted copy) before every prepared upgrade; real value never appears in any log |
| `$SILVERTASK_INSTALL_DIR/source` | The git checkout the *legacy* full-update path builds from | Yes — this is what an activation replaces | Not persistent application data; safe to be replaced by design |
| `$SILVERTASK_INSTALL_DIR/publish` | The currently-running build output | Yes — this is what an activation replaces | Not persistent application data; the legacy path already keeps `.previous` around during a swap |
| `$SILVERTASK_INSTALL_DIR/upgrade-staging/<version>` | A prepared-but-not-activated release | N/A — new territory Phase 52/53 introduced | Isolated by construction (a separate `git worktree`); never the source an activation reads from until a future phase says so |
| `$SILVERTASK_INSTALL_DIR/{installed-version.json,upgrade-state.json}` | Version/upgrade bookkeeping | Only the upgrade engine itself writes these | Not secret; readable without root in spirit (currently root-only file permissions, matching everything else under the install dir) |
| `/var/backups/silver-task/<timestamp>/` | Backup sets (database dump, attachments archive, config copy, manifest) | Never by an upgrade — only ever created, never modified by anything except its own retention cleanup | Restricted permissions (`700`/`600`); retention (`scripts/backup-debian.sh`) never deletes the newest backup or one linked to an in-progress upgrade; only ever deletes directories matching its own naming pattern |

## Migration safety classification

Every prepared upgrade's migration plan is classified as one of:

| Classification | Meaning | When |
|---|---|---|
| `SAFE` | No pending schema migrations, and the release doesn't declare a required data migration | Target release adds no new EF Core migrations relative to what's already applied, and its `releases/<version>.json` (if present) doesn't set `requiresDataMigration: true` |
| `REQUIRES_BACKUP` | Pending schema migrations exist | At least one EF Core migration in the target release hasn't been applied yet — exactly what the pre-upgrade database backup above exists to protect against |
| `REQUIRES_MAINTENANCE_MODE` | The release declares a required data migration | `releases/<version>.json` sets `requiresDataMigration: true` — an application-level data transformation beyond a schema change, which a future activation phase should not run against live traffic |
| `REQUIRES_MANUAL_REVIEW` | Reserved for a future phase | Not automatically assigned in Phase 53 — malformed metadata or an unresolvable migration state already blocks the upgrade outright (exit codes 14–16) rather than being classified and allowed through |

This classification is informational/planning-only in Phase 53 — nothing currently *acts*
differently based on it (e.g. no maintenance-mode toggle exists yet). A future activation phase is
expected to branch on it.
