# Release channels, maintenance window & release history (Phase 56)

**This document's scope was inferred, not taken from an original spec.** The real Phase 56 spec was
never received intact — three consecutive pastes of it were cut off identically, mid-sentence, with
the actual numbered requirements missing. Rather than block indefinitely or invent unrelated
task-manager features, this phase's scope was inferred from the phase title
("Upgrade Management, Release Channels & Production Hardening"), the pattern established by Phases
51–55, and two directly-confirmed design decisions (stable-only-by-default release channels with an
explicit opt-in for pre-release, and an opt-in maintenance-window policy). If this doesn't match what
you actually asked for, say so — everything here is easy to adjust or remove.

This document covers the four things Phase 56 added to `sudo ./scripts/update-debian.sh`. For the
command reference (flags, exit codes) see README → [Upgrade Engine](../README.md#upgrade-engine) →
["Release channels, maintenance window & preflight"](../README.md#release-channels-maintenance-window--preflight).
None of it changes any Phase 51–55 default behavior — every new mechanism here is opt-in.

## Release channels

Before Phase 56, `--latest`/`--target-version` discovery only ever considered git tags matching
`vMAJOR.MINOR.PATCH` exactly — pre-release tags (`v1.1.0-beta`, `v1.1.0-rc1`) were excluded outright,
with no way to opt in even deliberately. `releases/<version>.json` already had a `"channel"` field
(e.g. `releases/1.0.1.json` declares `"channel": "stable"`), but it was never actually usable as a
selector — any value other than the literal string `"stable"` was rejected as malformed metadata.

Phase 56 makes channel a real, opt-in selector:

- **`stable`** is the unconditional default — unset `--channel` and unset `Upgrade__Channel` both
  resolve to it, and its behavior is byte-identical to pre-Phase-56: only `vMAJOR.MINOR.PATCH` tags,
  numeric sort, no pre-release tags ever surfaced.
- **`beta`** additionally surfaces pre-release tags. Selecting it is always explicit — either
  `--channel=beta` on the command line, or `Upgrade__Channel=beta` in `silvertask.env` (the command-
  line flag always wins if both are set). There is no environment or heuristic that switches an
  installation to `beta` on its own.

```bash
sudo ./scripts/update-debian.sh --channel=beta --check              # is a newer beta release available?
sudo ./scripts/update-debian.sh --channel=beta --latest             # stage the latest beta (or stable) release
sudo ./scripts/update-debian.sh --channel=beta --target-version 1.1.0-beta
```

**Two independent guards prevent a pre-release from ever reaching a stable installation by
accident:**

1. **Target-version string validation.** A pre-release version string (`MAJOR.MINOR.PATCH-identifier`)
   is only ever accepted as a `--target-version` when the *effective* channel (flag → env var →
   default) is `beta`. On the default `stable` channel, `--target-version 1.1.0-beta` is rejected
   outright as an invalid version (exit `2`) — never silently coerced, never treated as equivalent to
   `1.1.0`.
2. **Declared-metadata cross-check.** Even for a `--target-version` string that *looks* like a plain
   stable version (no `-` suffix), `cmd_prepare` reads the resolved release's own
   `releases/<version>.json` and compares its declared `"channel"` field against the effective
   operating channel. A release whose metadata declares `"channel": "beta"` can never be selected
   while operating on `stable` — this is defense in depth against a release's tag and its declared
   metadata channel disagreeing, not just against the tag format itself (exit `37` on mismatch).

Ordering (same-version / downgrade detection) compares only the `MAJOR.MINOR.PATCH` part of a
version — a pre-release tag like `1.1.0-beta` sorts identically to `1.1.0` for upgrade-path purposes.
Comparing pre-release identifiers against each other (e.g. deciding `1.1.0-beta` < `1.1.0-rc1`) is
out of scope for this engine.

## Maintenance window policy

Unset by default — `--activate`/`--rollback` behave exactly as they did in Phases 54/55, runnable at
any time once confirmed. Set `Upgrade__MaintenanceWindow` in `silvertask.env` to restrict *when*
they're allowed to run:

```bash
# silvertask.env
Upgrade__MaintenanceWindow=02:00-04:00
```

Format is `HH:MM-HH:MM`, 24-hour, server-local time. A window that wraps midnight is supported
(e.g. `22:00-04:00` means "any time from 10pm through 4am"). A malformed value is **not** a hard
failure — it's logged as a warning and treated as unset, so a typo never accidentally locks out every
future upgrade.

When configured, `--activate` and `--rollback` check the current time against the window **after**
the existing `[y/N]` confirmation prompt and **before** acquiring the upgrade lock (so a
declined-outside-window attempt never even contends for the lock). Outside the window:

```text
UPGRADE BLOCKED BY MAINTENANCE WINDOW POLICY
Upgrade__MaintenanceWindow=02:00-04:00 is configured and the current time is outside it.
Pass --override-maintenance-window to proceed anyway (requires confirmation).
```

exit `35`. `--override-maintenance-window` proceeds anyway, but only after its own typed
confirmation — the same `st_confirm_destructive` mechanism the rollback database-restore step
already uses (Phase 55) — so an override is always a deliberate, logged decision, never a silent
bypass.

```bash
sudo ./scripts/update-debian.sh --activate --override-maintenance-window   # requires typed confirmation
```

`--doctor` (below) reports the maintenance-window policy's current state, including whether *right
now* is inside or outside it, so an operator can check before attempting an activation.

## Release history

Before Phase 56, `upgrade-state.json` and `rollback-state.json` each held only the **single most
recent** attempt of their kind — overwritten every time, with no durable record of what happened to
an installation across its whole lifetime. Phase 56 adds an append-only log,
`$SILVERTASK_INSTALL_DIR/release-history.jsonl`, one compact JSON object per line (JSON Lines) —
appending a line never requires rewriting the file, unlike the single-slot state files.

```bash
sudo ./scripts/update-debian.sh --history              # most recent 20 entries
sudo ./scripts/update-debian.sh --history --limit=50   # most recent 50
```

```text
2026-03-05T02:14:07Z  [upgrade] 1.0.1 -> 1.1.0  COMPLETED  (id: upgrade-20260305-021211-a1b2c3)
2026-02-20T03:41:52Z  [rollback] 1.1.0 -> 1.0.1  COMPLETED  (id: rollback-20260220-033812-d4e5f6)  reason: Health check failure
2026-02-20T03:20:09Z  [upgrade] 1.0.1 -> 1.1.0  FAILED  (id: upgrade-20260220-031544-b7c8d9)  reason: exit code 22
```

Only real, terminal outcomes are recorded — a line is appended exactly once per `--activate`/
`--rollback` invocation, at its `COMPLETED` or `FAILED` exit, whichever it reaches. `--check`,
`--status`, `--latest`/`--target-version` (prepare), and `--dry-run` never write to it, since none of
them change anything about the running installation. The file is created `640` (root-owned,
group-readable) — like the other state files, it contains no secrets, but is still restricted to
root by default.

## Preflight check (`--doctor`)

Read-only, modifies nothing:

```bash
sudo ./scripts/update-debian.sh --doctor
```

Checks, in order: required tools on `PATH` (`git`, `dotnet`, `pg_dump`, `pg_restore`, `curl`,
`openssl`), whether `dotnet-ef` is restorable from the pinned tool manifest, whether the environment
file exists with its required keys present (the same key-presence check `backup-debian.sh` already
does — values are never printed), installed/running version consistency, whether the upgrade lock or
maintenance mode is currently held/active, the maintenance-window policy's current state, and disk
space at the install location.

Each check prints `PASS`, `WARN`, or `FAIL`. Only `FAIL` blocks the summary exit code (`36`) — a held
upgrade lock or active maintenance mode is reported as `WARN`, not `FAIL`, since it may simply mean a
real upgrade is legitimately in progress right now rather than a stuck/broken state.

## Log rotation

`/var/log/silver-task-install.log` and `/var/log/silver-task/upgrade.log` previously grew forever.
`install-debian.sh` now also installs `deploy/silvertask-logrotate` to `/etc/logrotate.d/silvertask`
(weekly rotation, 12 kept, compressed) — the standard Debian mechanism for this, not custom rotation
logic layered onto `st_up_log`. Rotation itself runs via the distro's existing `logrotate`
cron/systemd timer; nothing in this codebase invokes it directly.

## What this phase deliberately did not add

Consistent with every earlier phase's scope discipline: no web-based upgrade UI, no automatic
scheduling/cron integration for upgrades themselves (the maintenance window only *gates* a manually-
invoked `--activate`/`--rollback`, it doesn't trigger one), no multi-generation rollback history (that
remains Phase 55's single most-recent-activation limitation), and no new backup/restore mechanism —
release history and backups remain two separate, purpose-built things.
