# Automated upgrade testing & release certification (Phase 57)

**This document's scope was inferred, not taken from an original spec.** The real Phase 57 spec
was truncated identically twice — cutting from "## Primary Goal" straight to a trailing success/
failure-message fragment, with every actual numbered requirement missing. This plan was inferred
from the phase title, the one goal sentence that did come through ("create a repeatable testing
and certification system that verifies releases can be safely installed, upgraded, validated, and
rolled back before being considered ready for the stable production channel"), and the pattern
established by Phases 51–56. If this doesn't match what you actually asked for, say so.

## What this is

`scripts/certify-release.sh` is a new, separate top-level script — not another
`update-debian.sh` mode — that orchestrates a full **install → upgrade → validate → rollback →
validate** lifecycle test, using the *existing* `install-debian.sh` / `update-debian.sh` /
`uninstall-debian.sh` exactly as an operator would run them by hand. It produces a durable,
independently-verified certification report a maintainer or CI can check before tagging/publishing
a release candidate to the stable channel.

**Certification is report-only.** It does not automatically gate anything in the runtime upgrade
engine — `cmd_prepare`'s channel logic is untouched, and `releases/<version>.json` is never
stamped. A `CERTIFIED` verdict is a durable, checkable fact you consult manually (or from CI)
before deciding to tag a release, not something the engine itself enforces.

## ⚠️ Disposable hosts only — never production

**`certify-release.sh` installs, upgrades, rolls back, and (with `--cleanup`) uninstalls Silver
Task on whatever host it runs on.** It requires an explicit `--disposable-host-confirmed` flag
(no default — omitting it is an argument error, before anything touches the host) and, unless
`--yes` is also passed, a second, typed interactive confirmation. There is no auto-detection of
"is this host really disposable" — that would be exactly the kind of guess this project's other
destructive operations (rollback's database-restore decision, Phase 56's maintenance-window
override) deliberately avoid. **Only run this against a spare VM or container you are willing to
have fully rebuilt.**

## Prerequisites

- A real, disposable Debian 12+ host — apt, systemd, root, and a real PostgreSQL instance are all
  required, the same as `install-debian.sh` itself needs. This cannot be exercised inside a
  sandboxed/CI environment without a real Debian target (see "Limitations" below).
- A git clone of this repository on that host, with network access to fetch tags from
  `SILVERTASK_REPO_URL` (the same GitHub remote `update-debian.sh` already uses).
- The candidate release (and, if given explicitly, the baseline release) must already be pushed as
  a git tag — certification tests a release that already exists, it doesn't create one.

## Usage

```bash
sudo ./scripts/certify-release.sh --candidate=1.2.0 --disposable-host-confirmed

sudo ./scripts/certify-release.sh --candidate=1.2.0 --baseline=1.1.0 --channel=beta \
    --disposable-host-confirmed --yes --cleanup
```

| Flag | Meaning |
|---|---|
| `--candidate=X.Y.Z` | Required. The release being certified. |
| `--baseline=X.Y.Z` | Optional. The version installed first and upgraded *from*. Defaults to the latest discovered stable release (same discovery `--latest` uses). |
| `--channel=stable\|beta` | Optional, default `stable`. Passed through to `update-debian.sh --channel` for the candidate prepare/activate stages, so a beta candidate can be certified too. |
| `--disposable-host-confirmed` | Required. The explicit, unambiguous "this host is expendable" acknowledgment. |
| `--yes` | Skips the interactive typed confirmation, for CI. Still requires `--disposable-host-confirmed` — a flag is never a substitute for the other. |
| `--cleanup` | Runs `uninstall-debian.sh --remove-data --force` at the end, regardless of outcome. Default: leave the installation in place, so a failed run can be investigated. |

## Lifecycle stages

Each stage records `PASS`/`FAIL`/`SKIPPED`, its exit code, and a timestamp to the certification
report. A later stage never runs after an earlier one has failed — there's no point validating a
rollback that never happened.

1. **`baseline_install`** — checks out `v<baseline>` in the local repo clone and runs
   `install-debian.sh --non-interactive --skip-ssl --skip-firewall` (no domain — a disposable host
   has no real DNS to terminate TLS for).
2. **`baseline_health`** — independently re-confirms `GET /api/health/ready` and the reported
   version match `--baseline`, rather than only trusting `install-debian.sh`'s own internal check.
3. **`candidate_prepare`** — `update-debian.sh --target-version <candidate> --channel=<channel>
   --yes` (stops at `READY_FOR_ACTIVATION`; this is where an invalid/unavailable/wrong-channel
   candidate is caught).
4. **`candidate_activate`** — `update-debian.sh --activate --yes`.
5. **`candidate_validate`** — independently re-confirms health/version now match `--candidate`.
6. **`rollback`** — `update-debian.sh --rollback --yes`.
7. **`rollback_validate`** — independently re-confirms `--baseline` is actually back and healthy.
8. **`cleanup`** (only with `--cleanup`) — `uninstall-debian.sh --remove-data --force`. Reported,
   but its outcome never changes the verdict — the verdict is already written (see below) before
   cleanup ever runs.

Stages 2, 5, and 7 are deliberately **not** redundant with `update-debian.sh`'s own internal
health/version validation inside `--activate`/`--rollback` — this is an external, independent
re-check, the whole point of a certification harness being not to just trust the tool under test's
own self-report.

## Reading the report

`$SILVERTASK_CERTIFICATION_DIR/certification-<candidate>-<certId>.jsonl` (default
`/var/log/silver-task/certifications/`), JSON Lines — same append-only, one-object-per-line
convention as Phase 56's `release-history.jsonl`, never a hand-built JSON array:

```json
{"type":"run","certId":"certify-20260305-021211-a1b2c3","candidate":"1.2.0","baseline":"1.1.0","channel":"stable","startedAtUtc":"2026-03-05T02:12:11Z"}
{"type":"stage","name":"baseline_install","status":"PASS","exitCode":"0","detail":"","recordedAtUtc":"2026-03-05T02:14:02Z"}
{"type":"stage","name":"baseline_health","status":"PASS","exitCode":"0","detail":"","recordedAtUtc":"2026-03-05T02:14:15Z"}
...
{"type":"verdict","verdict":"CERTIFIED","failedStage":"","completedAtUtc":"2026-03-05T02:19:40Z"}
```

A `NOT_CERTIFIED` verdict line includes the exact `failedStage` — the first stage that failed (all
work stops there, so it's always the only failure recorded). Full command output for every stage is
also written to `SILVERTASK_CERTIFICATION_LOG_FILE` (default `/var/log/silver-task/certification.log`).

## Exit codes

This is a separate script with its own small, independent exit-code scheme — it does not continue
`update-debian.sh`'s numbering (37 and below), since it's a different process orchestrating that
script rather than another mode of it.

| Code | Meaning |
|---|---|
| 0 | `CERTIFIED` — every required stage passed |
| 1 | General error |
| 2 | Invalid arguments, or the disposable-host safety gate was not satisfied |
| 3 | `baseline_install` failed |
| 4 | `baseline_health` failed |
| 5 | `candidate_prepare` failed |
| 6 | `candidate_activate` failed |
| 7 | `candidate_validate` failed |
| 8 | `rollback` failed |
| 9 | `rollback_validate` failed |

## How this fits into the release process

Run `certify-release.sh` against a release candidate's tag, on a disposable host, before tagging it
for wider consumption or promoting it to the stable channel. A `CERTIFIED` report is the evidence
that the full lifecycle — a real install, a real upgrade, real health/version validation, and a
real rollback — actually works for that specific candidate, not just that its code compiles.

## Limitations

- **Cannot be exercised from a sandboxed/non-Debian environment** — like every other
  Debian-specific mechanic in this project (Phases 51–56), the actual `install`/`upgrade`/
  `rollback` subprocess calls require real `apt`, `systemd`, `root`, and PostgreSQL. Only the
  portable bookkeeping/reporting logic (`scripts/lib/certify.sh`) and `certify-release.sh`'s own
  argument parsing/safety gate are covered by `scripts/test-certify-release.sh`.
- Certifies one upgrade path at a time (`--baseline` → `--candidate`); it does not attempt every
  historically-supported upgrade path in one run.
- No automatic promotion — see "What this is" above. A `CERTIFIED` report never tags, pushes, or
  publishes anything by itself.
