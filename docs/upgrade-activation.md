# Upgrade activation (Phase 54)

This document covers `sudo ./scripts/update-debian.sh --activate` — the command that actually
replaces the running application, runs migrations, and commits the new installed version. For the
command reference (flags, exit codes) see README → [Upgrade
Engine](../README.md#upgrade-engine). For the preparation workflow that must run first, see
[upgrade-safety.md](upgrade-safety.md).

## Prerequisites

`--activate` refuses to run unless a prior `--latest`/`--target-version` already succeeded and
left the installation `READY_FOR_ACTIVATION`, with every safety-pipeline stage recorded `OK`:

```text
Upgrade ID exists
Target version recorded
Database backup created and verified
Configuration backup created and verified
Persistent data check passed
Migration state validated
Target migration validation passed
Staged release still present on disk
Referenced backup still present on disk
```

If any of these is missing, `--activate` prints `UPGRADE ACTIVATION BLOCKED` with the specific
reason and exits `17` — it never silently skips a safety check or falls back to preparing on the
fly.

## Preparing an upgrade

```bash
sudo ./scripts/update-debian.sh --check                # is there a newer stable release?
sudo ./scripts/update-debian.sh --dry-run --latest      # see the full plan, change nothing
sudo ./scripts/update-debian.sh --latest                # or --target-version X.Y.Z
```

The last command validates the target, creates and verifies a full pre-upgrade backup, checks
persistent-data placement, and validates/plans any required migration — ending in
`READY_FOR_ACTIVATION` if everything passes. See [upgrade-safety.md](upgrade-safety.md) for the
full breakdown of this step.

## Reviewing upgrade status

```bash
sudo ./scripts/update-debian.sh --status
```

Reports installed/running version consistency, whether a newer stable release exists, and the
upgrade engine's own state — `IDLE`, `READY_FOR_ACTIVATION` (with backup/migration/persistent-data
status), `IN PROGRESS` (a process currently holds the lock), `STALE UPGRADE LOCK DETECTED` (a
*prepare*-stage attempt was interrupted — safe to retry), `INTERRUPTED UPGRADE DETECTED` (an
*activation*-stage attempt was interrupted while maintenance mode was on — see below), `COMPLETED`,
or `FAILED`. Read-only — never modifies anything, regardless of what it reports.

## Activating an upgrade

```bash
sudo ./scripts/update-debian.sh --activate
```

Displays a confirmation panel — current/target version, upgrade ID, backup verification status,
whether a migration is required — and requires an explicit `y`/`Y` (default **N**; skip the prompt
only with `--activate --yes`, e.g. for scripted/scheduled activation windows). Once confirmed:

1. **Builds the target release into a fresh directory** (`git checkout` the target tag in the
   source checkout, `dotnet publish` into `<publish-dir>.new`) — the running application is
   completely untouched by this step. A build failure here aborts cleanly; nothing was ever
   stopped.
2. **Enables maintenance mode** — the still-running old application immediately starts returning
   `503` to everything except `/api/health*` (see "Maintenance mode behavior" below).
3. **Stops the service**, then swaps the publish directory in (`mv` the live directory aside to
   `<publish-dir>.previous` — kept, never deleted — then `mv` the freshly built one into place).
4. **Runs the migration** — `dotnet ef database update`, the project's own official migration
   command, nothing invented. A failure here stops immediately; nothing further runs (see
   "Migration behavior" below).
5. **Starts the service**, then polls `/api/health/ready` with bounded retries (15 attempts, 3s
   apart — 45s total, same budget the legacy update path already uses).
6. **Validates version consistency** — the running backend's own `GET /api/health` must report the
   target version exactly; a best-effort check also looks for the target version string inside the
   served frontend bundle (not fatal if undetectable, but a confirmed mismatch is).
7. **Runs smoke tests** — confirms the SPA shell (`GET /`) is actually served. No authenticated
   calls are made anywhere in this process — there is no service account to make one safely with,
   and production data is never touched merely to test the upgrade.
8. **Only now** commits `installed-version.json` with the new version.
9. **Disables maintenance mode** and runs one final availability check with normal traffic
   restored.

If every step succeeds: `UPGRADE COMPLETE`, with a printed timeline and the upgrade state recorded
`COMPLETED`. `--activate` cannot be combined with `--dry-run` — the confirmation prompt above is
already activation's own safety gate; there is no "preview an activation" mode, only "prepare
first, then decide."

## Maintenance mode behavior

A small ASP.NET Core middleware (`MaintenanceModeMiddleware`) checks for the existence of a flag
file (`Maintenance__FlagFile` in `silvertask.env`, default `/opt/silver-task/maintenance.json` —
deliberately outside the publish directory, so it survives the swap in step 3 above) on every
request. While present: every route except `GET /api/health*` returns `503` with `Retry-After: 30`
and a generic JSON body — never the upgrade ID, target version, or any other internal detail (those
live only in the flag file itself, read by this script, never echoed to a public HTTP response).
Enabled *before* the service is stopped (so the old app itself starts serving the maintenance
response for its last few requests) and disabled only after the new version is proven healthy,
version-consistent, and smoke-tested.

## Migration behavior

Migrations run exactly once, via the project's own `dotnet ef database update`, only after: backup
verified, persistent data checked, target migrations validated, maintenance mode active, and the
new release already swapped in (so the schema being migrated to matches the code that's about to
run against it). A migration failure is fatal and immediate — no further migrations run, the
installed version is not changed, maintenance mode stays active, and the database is **not**
automatically restored (Phase 54 does not implement automatic rollback; see
[restore.md](restore.md) for the manual, deliberate restore procedure using the pre-upgrade
backup).

## Health checks

`GET /api/health/ready` (checks database connectivity) is polled with bounded retries after the
service restarts — 15 attempts, 3 seconds apart, matching the same budget the legacy full-update
path already uses (`st_health_check` in `scripts/lib/common.sh`). Exhausting the retry budget
without a successful response is `HEALTH_CHECK_FAILED` (exit `22`) — maintenance mode stays active
for manual investigation; the timeout is never extended indefinitely.

## Success criteria

An activation is only reported `UPGRADE COMPLETE` after **all** of: target release built and
swapped in, migration completed (or none was required), service started, health checks passed,
backend version confirmed equal to target, frontend version confirmed or unconfirmable-but-backend-
already-proven, smoke tests passed, installed version committed, maintenance mode disabled, and the
final post-restoration availability check passed. Any single failure anywhere in that chain means
the installed version is **not** changed and the upgrade is recorded `FAILED`, not `COMPLETED`.

## Failure handling

Every failure path prints a **recovery information** block — previous/target version, upgrade ID,
backup location, and an explicit "the system has NOT marked version X.Y.Z as installed" — and
records the specific failure state (`ACTIVATION_FAILED`, `MAINTENANCE_MODE_FAILED`,
`SERVICE_START_FAILED`, `MIGRATION_FAILED`, `HEALTH_CHECK_FAILED`, `VERSION_VALIDATION_FAILED`,
`SMOKE_TEST_FAILED`) rather than a generic error. See README's exit-code table for the exact code
each maps to. **Phase 54 does not implement automatic rollback or recovery** — a failure after
maintenance mode was enabled leaves it enabled (deliberately: serving a clear maintenance response
is safer than serving a possibly-broken app) and requires manual investigation using
`systemctl status silvertask`, `journalctl -u silvertask`, and
`/var/log/silver-task/upgrade.log`.

## Interrupted upgrade detection

If the server reboots, loses power, or the activation process is killed mid-way, no automatic
resume or cleanup happens. The next `sudo ./scripts/update-debian.sh --status` detects this
specifically — a maintenance flag file that's still active with no process holding the upgrade
lock is unambiguous evidence of an interrupted activation — and reports `INTERRUPTED UPGRADE
DETECTED` (upgrade ID, previous/target version, last recorded step, and that maintenance mode is
still active) with exit code `25`, distinct from the lighter-weight `STALE UPGRADE LOCK DETECTED`
Phase 53 already reports for an interrupted *prepare* attempt (where maintenance mode was never
touched). Resolve manually — check service/journal status, decide whether to retry `--activate`
once the underlying issue is fixed, or restore from the pre-upgrade backup per
[restore.md](restore.md) — before doing anything else.
