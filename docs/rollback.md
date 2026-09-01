# Upgrade rollback & recovery (Phase 55)

**Warning: database restore discards data.** If the rollback you're about to run requires
restoring the pre-upgrade database backup (see "Database restore rollback" below), any data
created or modified in the database *after* that backup was taken — during or after the failed
upgrade — will be lost. An emergency backup of the current (pre-rollback) database state is always
taken first specifically so that data isn't discarded silently, but restoring *that* emergency
backup afterward is a separate, manual, deliberate decision — it never happens automatically.

This document covers `sudo ./scripts/update-debian.sh --rollback` — the command that undoes a
failed, interrupted, or unwanted activation. For the command reference (flags, exit codes) see
README → [Upgrade Engine](../README.md#upgrade-engine). For activation itself, see
[upgrade-activation.md](upgrade-activation.md).

## When to use rollback

- **`--activate` failed** (exit codes 18–24) after it had already switched the release — the
  running application is now on the broken target version, or the service won't start.
- **An activation was interrupted** (server reboot, power loss, killed process) with maintenance
  mode left active — `--status` reports `INTERRUPTED UPGRADE DETECTED`.
- **Administrator-requested rollback** — the upgrade technically "succeeded" (`UPGRADE COMPLETE`)
  but a problem was discovered afterward (a bug, unexpected behavior) and you want to revert while
  it's investigated.

Rollback only ever targets the **single most recent** activation — there is one preserved
`<publish-dir>.previous` slot and one linked pre-upgrade backup, not a multi-generation history.
Once you've activated again (or rolled back once), that slot is gone.

## Rollback eligibility

`--rollback` refuses to run unless there is genuinely something to roll back to. It checks the
*last recorded activation attempt* (`upgrade-state.json`, the same record `--activate` writes) —
never a second "release history" mechanism — for:

```text
A recorded attempt exists with both a previous and target version
That attempt's activationStatus is OK — i.e. the release switch actually happened
${SILVERTASK_PUBLISH_DIR}.previous still exists on disk
That attempt's pre-upgrade backup (and its manifest) still exists on disk
```

If the last attempt never got as far as switching the release (e.g. it failed while just
*building* the target), there is nothing to roll back — the running application was never changed
in the first place, and `--rollback` says so rather than guessing at what to do. This is reported
as `ROLLBACK BLOCKED` (exit `26`), or specifically `ROLLBACK TARGET UNAVAILABLE` (exit `27`) when
the preserved previous release is missing.

## The rollback command

```bash
sudo ./scripts/update-debian.sh --rollback                          # roll back, with a [y/N] prompt
sudo ./scripts/update-debian.sh --rollback --dry-run                # show the plan, change nothing
sudo ./scripts/update-debian.sh --rollback --reason="Health check failure"  # record why (optional)
sudo ./scripts/update-debian.sh --rollback --restore-config         # also restore configuration
sudo ./scripts/update-debian.sh --rollback --yes                    # skip confirmation (automation)
```

It prints a plan (current/target version, related upgrade ID, backup availability, the exact
numbered steps it will take, and the database-restore decision — see below) before asking
`Continue with rollback? [y/N]` — default **N**. If the decision is `DATABASE_RESTORE_REQUIRED`, a
**second**, stronger confirmation follows: you must type the exact rollback target version to
proceed, with an explicit warning about data loss. `--yes` skips both prompts (still printing the
plan/warnings first) for scripted use — never use it without having already reviewed a dry run.

## Application-only vs. database-restore rollback

Every upgrade attempt already records whether it required a database migration
(`migrationRequired`, set during `--latest`/`--target-version`'s migration-planning step — see
[upgrade-safety.md](upgrade-safety.md)). Rollback reuses that exact recorded fact — never a new
compatibility matrix, never a guess:

| `migrationRequired` | Decision | What happens |
|---|---|---|
| `false` | `APPLICATION_ONLY_ROLLBACK` | Only the application release is switched back. The database schema was never changed, so the previous application version already matches it. |
| `true` | `DATABASE_RESTORE_REQUIRED` | The application release is switched back **and** the pre-upgrade database backup is restored (after an emergency backup of the current state) — the schema the failed upgrade's migration added would otherwise be incompatible with the older application code. |
| missing/corrupted | `MANUAL_RECOVERY_REQUIRED` | Rollback is **blocked** (exit `26`) rather than guessing which way is safe. See "Manual recovery" below. |

## Emergency backups

Before any database restore, `--rollback` creates its own backup of the **current** (post-failure)
database state — reusing the exact same `scripts/backup-debian.sh` mechanism and verification as
every other backup in this project, tagged `emergency-pre-rollback` and linked to the rollback ID.
A failed or unverified emergency backup **blocks the rollback** (exit `28`) — the current database
is never touched without first being preserved. The narrow escape hatch,
`--force-no-emergency-backup`, requires its own typed confirmation and is not recommended; use it
only when you've already independently verified the current data doesn't matter (e.g. it's known
to be corrupt).

## Database restore rollback

`pg_restore --clean --if-exists --no-owner` against the pre-upgrade backup's `database.dump` — the
same tool, same connection credentials (`ConnectionStrings__DefaultConnection`), and same
`--tag=`-based manifest linkage `scripts/backup-debian.sh` already uses to *create* backups, run in
reverse. `--clean --if-exists` drops and recreates the application's own schema objects in place;
the application's database user already owns everything in its database, so no elevated database
privileges are required. After restoring, the database is validated (a real, read-only connection
confirming the restored schema has no migration pending relative to the rollback target's own
code, using the exact same check `--latest`/`--activate` already use) before the rollback is
allowed to proceed — a restore that "succeeds" but produces an incompatible database is treated as
a failure (`DATABASE_RESTORE_FAILED`, exit `29`), not a silent success.

## Configuration rollback

`--activate` never modifies `/etc/silvertask/silvertask.env` — so by default, **rollback never
touches configuration either**; there's nothing an automated upgrade could have changed. Pass
`--restore-config` only if you know configuration was manually changed around the time of the
failed upgrade and want it reverted too. When used: the current configuration is copied aside first
(`silvertask.env.emergency-<timestamp>`, permissions restricted), then the pre-upgrade backup's
copy is restored over it. Secret values are never printed or logged at any point.

## Health validation

After the release (and, if required, the database) is switched, `--rollback` restarts the service
and runs the exact same validation `--activate` does: `GET /api/health/ready` with bounded retries
(`HEALTH_CHECK_FAILED`, exit `32`, on timeout), backend version confirmation against the rollback
target (`VERSION_VALIDATION_FAILED`, exit `33`, on mismatch), and a `GET /` smoke test
(`SMOKE_TEST_FAILED`, exit `24` — the same code `--activate`'s own smoke-test failure already
uses). The installed version is committed only after all of these pass — never before.

## Failure handling

Every failure prints a recovery-information block (failed step, current active release, database
state, configuration state, available backups including the emergency one, maintenance mode
status) and keeps maintenance mode **active** — Phase 55 does not attempt automatic recovery from a
failed rollback. Administrator review is required; see `/var/log/silver-task/upgrade.log` for the
exact command output at the point of failure.

## Interrupted rollback detection

If the server reboots or the rollback process is killed mid-way, `sudo ./scripts/update-debian.sh
--status` detects it — a maintenance flag still active with no process holding the upgrade lock,
whose recorded ID starts with `rollback-` — and reports `INTERRUPTED ROLLBACK DETECTED` (exit
`34`), distinct from an interrupted *activation*. Nothing is automatically resumed or cleaned up;
resolve manually (check `systemctl status silvertask`, `journalctl -u silvertask`, and the upgrade
log) before retrying.

## Manual recovery

If rollback itself is blocked (`MANUAL_RECOVERY_REQUIRED`, or any `ROLLBACK FAILED` state) and you
need to recover by hand:

1. Stop the service: `sudo systemctl stop silvertask`.
2. Decide which release should be active — check `${SILVERTASK_PUBLISH_DIR}.previous` (the
   pre-failure release, if not yet consumed) and `${SILVERTASK_PUBLISH_DIR}.failed` (the release
   that was rolled back *from*, preserved for troubleshooting — see its sidecar
   `${SILVERTASK_PUBLISH_DIR}.failed.json` for what it was and why it's there).
3. If a database restore is needed, follow [restore.md](restore.md)'s isolated-verification
   procedure first, then its production-restore steps — never restore blind.
4. Manually swap the desired release into `$SILVERTASK_PUBLISH_DIR`, `chown -R silvertask:silvertask`
   it, and `sudo systemctl start silvertask`.
5. Verify with `curl -f http://127.0.0.1:5000/api/health/ready` before disabling maintenance mode
   (`sudo rm /opt/silver-task/maintenance.json`, or wherever `Maintenance__FlagFile` points).
