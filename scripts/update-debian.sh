#!/bin/bash
# Silver Task — Debian update / upgrade-engine script.
#
# Two distinct things live in this one file:
#
#   1. The LEGACY full-update path (no upgrade-engine flags — just today's usage, optionally with
#      --skip-backup/--ref=) — UNCHANGED since Phase 51: backs up, fetches/checks out the latest
#      source in place, rebuilds, migrates, restarts, and health-checks. This is what actually
#      deploys a change; Phase 52 does not alter a single line of its behavior.
#   2. The upgrade ENGINE (--check/--status/--latest/--target-version/--activate/--rollback,
#      optionally with --dry-run/--yes) — Phase 52 built discovery/validation/staging; Phase 53
#      added the safety layer that must exist before anything can activate: a verified pre-upgrade
#      backup (database + attachments + configuration, via scripts/backup-debian.sh), a
#      persistent-data placement check, and migration discovery/validation/planning via the
#      project's own EF Core CLI — never executing a migration. A successful --latest/
#      --target-version run ends in READY_FOR_ACTIVATION, still without activating anything.
#      Phase 54's --activate is what actually does: maintenance mode, an atomic-as-practical
#      release swap, running the real migration, restarting the service, health/version
#      validation, and only then committing installed-version.json. Phase 55's --rollback is the
#      undo path: reactivates the preserved previous release (and, only if the last upgrade
#      actually required a migration, restores the verified pre-upgrade database backup — after
#      first taking an emergency backup of the current, failed state) with the same
#      maintenance-mode/health/version-validation discipline as activation. Phase 56 adds release
#      management on top, all opt-in/zero-default-behavior-change: --channel selects between the
#      "stable" (default, unchanged) and "beta" (pre-release) release channels; an optional
#      Upgrade__MaintenanceWindow policy can require --activate/--rollback to run inside a
#      configured window; --history shows a durable log of every past activation/rollback; and
#      --doctor is a read-only preflight check of the whole toolchain. See README "Upgrade Engine"
#      for the full explanation.
#
# Usage:
#   sudo ./scripts/update-debian.sh                                # legacy: update to latest + activate
#   sudo ./scripts/update-debian.sh --skip-backup                  # legacy, skip the pre-update backup
#   sudo ./scripts/update-debian.sh --ref=v1.0.1                   # legacy, update to a specific tag/branch
#   sudo ./scripts/update-debian.sh --check                        # is a newer stable release available?
#   sudo ./scripts/update-debian.sh --status                       # upgrade/rollback/version status report
#   sudo ./scripts/update-debian.sh --latest                       # validate, back up, and stage the latest stable release
#   sudo ./scripts/update-debian.sh --target-version 1.1.0         # validate, back up, and stage a specific release
#   sudo ./scripts/update-debian.sh --dry-run --latest             # show what --latest would do, change nothing
#   sudo ./scripts/update-debian.sh --target-version 1.1.0 --yes   # skip the confirmation prompt
#   sudo ./scripts/update-debian.sh --channel=beta --latest         # consider pre-release tags too (opt-in)
#   sudo ./scripts/update-debian.sh --activate                     # activate a prepared, READY_FOR_ACTIVATION release
#   sudo ./scripts/update-debian.sh --activate --yes                # activate without the confirmation prompt
#   sudo ./scripts/update-debian.sh --rollback                     # roll back to the previous release
#   sudo ./scripts/update-debian.sh --rollback --dry-run            # show the rollback plan, change nothing
#   sudo ./scripts/update-debian.sh --rollback --reason="..."       # record why (optional)
#   sudo ./scripts/update-debian.sh --history                      # show past activation/rollback history
#   sudo ./scripts/update-debian.sh --doctor                       # preflight-check the toolchain (read-only)
#   sudo ./scripts/update-debian.sh --help
#
# Exit codes (upgrade-engine modes: --check/--status/--latest/--target-version/--activate/
# --rollback/--history/--doctor; the legacy path always used a plain 0 = success / 1 = failure and
# still does):
#   0 success / no blocking problem        9 database backup failed        18 maintenance mode failure  27 rollback target unavailable
#   1 general error                       10 database backup verify failed 19 application activation/switch failure 28 emergency backup failed
#   2 invalid arguments                   11 configuration backup failed   20 service startup failure   29 database restore failed
#   3 version inconsistency               12 config backup verification failed 21 migration execution failure 30 configuration restore failed
#   4 target version unavailable          13 persistent data safety check failed 22 health check timeout 31 rollback service startup failed
#   5 unsupported upgrade path            14 current migration state invalid 23 version validation failure 32 rollback health check failed
#   6 upgrade/rollback already in progress 15 target migration validation failed 24 smoke test failure  33 rollback version validation failed
#   7 repository access failure           16 migration planning failed     25 interrupted upgrade detected (--status) 34 interrupted rollback detected (--status)
#   8 insufficient disk space             17 activation prerequisites missing 26 rollback eligibility failed
#   35 blocked by maintenance-window policy   36 preflight (--doctor) check failed   37 invalid/disallowed release channel
#
# Codes 6/18/19 are reused for the equivalent rollback failure categories (lock busy / maintenance
# mode / release-switch) — same meaning Phase 54 already gave them, not redefined.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"
# shellcheck source=lib/upgrade.sh
source "$SCRIPT_DIR/lib/upgrade.sh"
# shellcheck source=lib/rollback.sh
source "$SCRIPT_DIR/lib/rollback.sh"

st_up_usage() {
    cat <<'EOF'
Usage: sudo ./scripts/update-debian.sh [command] [options]

Legacy full update (unchanged since Phase 51 — actually deploys a change):
  (no command)              Update to the latest default-branch commit and activate it.
  --ref=<git-ref>           Update to a specific tag/branch instead, and activate it.
  --skip-backup             Skip the pre-update backup (not recommended).

Upgrade engine — prepare (--latest/--target-version), activate (--activate), and roll back
(--rollback) are separate, deliberately confirmed steps:
  --check                   Report whether a newer stable release is available. Read-only.
  --status                  Report installed/running version, upgrade, and rollback status. Read-only.
  --latest                  Validate, back up, and stage the latest stable release.
  --target-version X.Y.Z    Validate, back up, and stage a specific stable release.
  --dry-run                 With --latest/--target-version/--rollback: report only, change nothing.
  --activate                Activate a prepared (READY_FOR_ACTIVATION) release. See below.
  --rollback                Roll back to the previously active release. See below.
  --reason="..."            With --rollback: why (optional; recorded, never blocks if omitted).
  --restore-config          With --rollback: also restore configuration from the pre-upgrade backup.
  --force-no-emergency-backup  With --rollback: skip the pre-restore emergency DB backup (requires
                            typed confirmation — see docs/rollback.md; not recommended).
  --yes                     With --latest/--target-version/--activate/--rollback: skip confirmation.

Release channels, history, maintenance window, and preflight (Phase 56):
  --channel=stable|beta     With --check/--status/--latest/--target-version: which release channel
                            to consider. Default: stable (unchanged) — beta additionally surfaces
                            pre-release tags (X.Y.Z-identifier) and is never used implicitly.
  --history                 Show past activation/rollback history (most recent first). Read-only.
  --limit=N                 With --history: how many entries to show (default 20).
  --doctor                  Preflight-check the toolchain, configuration, and installation state.
                            Read-only — modifies nothing.
  --override-maintenance-window  With --activate/--rollback: proceed even if an
                            Upgrade__MaintenanceWindow policy is configured and the current time is
                            outside it (requires typed confirmation).

  --help, -h                Show this message.

--check and --status never modify anything. --latest/--target-version (without --dry-run) stage
the release into an isolated git worktree, create and verify a pre-upgrade backup (database +
attachments + configuration), check persistent-data placement, and validate/plan any required
migrations — but never replace the running application, run a migration, or restart services.

--activate requires a release already READY_FOR_ACTIVATION (i.e. a prior --latest/--target-version
succeeded and nothing has changed since). It enables maintenance mode, swaps in the prepared
release, runs any required migration, restarts the service, validates health/version, and only
then commits the installed version and disables maintenance mode.

--rollback requires the last upgrade attempt to have actually switched the active release (its
preserved previous release must still be on disk). It reactivates that previous release and, only
if that upgrade actually required a migration, restores the verified pre-upgrade database backup
(after first taking its own emergency backup of the current, failed database state) — with the
same maintenance-mode/health/version-validation discipline as --activate. See README "Upgrade
Engine" for the full explanation, docs/upgrade-activation.md for activation, and docs/rollback.md
for the full rollback workflow and its data-loss warnings.
EOF
}

# --- Argument parsing ---
MODE="legacy"              # legacy | check | status | prepare | activate | rollback | history | doctor | help
TARGET_SELECTOR=""         # "" | "latest" | "target-version"
TARGET_VERSION=""
DRY_RUN=false
ASSUME_YES=false
SKIP_BACKUP=false
REF=""
ROLLBACK_REASON=""
RESTORE_CONFIG=false
FORCE_NO_EMERGENCY_BACKUP=false
CHANNEL_FLAG=""             # "" | stable | beta — explicit --channel value, resolved later
HISTORY_LIMIT=20
LIMIT_EXPLICIT=false
OVERRIDE_MAINTENANCE_WINDOW=false

st_up_require_mode_legacy() {
    if [ "$MODE" != "legacy" ]; then
        st_up_usage >&2
        echo "ERROR: --check, --status, --latest, --target-version, --activate, --rollback, --history, and --doctor cannot be combined with each other." >&2
        exit 2
    fi
}

while [ $# -gt 0 ]; do
    case "$1" in
        --skip-backup) SKIP_BACKUP=true ;;
        --ref=*) REF="${1#*=}" ;;
        --check) st_up_require_mode_legacy; MODE="check" ;;
        --status) st_up_require_mode_legacy; MODE="status" ;;
        --latest)
            st_up_require_mode_legacy
            if [ -n "$TARGET_SELECTOR" ]; then
                st_up_usage >&2; echo "ERROR: --latest and --target-version are mutually exclusive." >&2; exit 2
            fi
            MODE="prepare"; TARGET_SELECTOR="latest"
            ;;
        --target-version)
            st_up_require_mode_legacy
            if [ -n "$TARGET_SELECTOR" ]; then
                st_up_usage >&2; echo "ERROR: --latest and --target-version are mutually exclusive." >&2; exit 2
            fi
            shift
            if [ $# -eq 0 ]; then
                st_up_usage >&2; echo "ERROR: --target-version requires a value, e.g. --target-version 1.1.0" >&2; exit 2
            fi
            MODE="prepare"; TARGET_SELECTOR="target-version"; TARGET_VERSION="$1"
            ;;
        --target-version=*)
            st_up_require_mode_legacy
            if [ -n "$TARGET_SELECTOR" ]; then
                st_up_usage >&2; echo "ERROR: --latest and --target-version are mutually exclusive." >&2; exit 2
            fi
            MODE="prepare"; TARGET_SELECTOR="target-version"; TARGET_VERSION="${1#*=}"
            ;;
        --dry-run) DRY_RUN=true ;;
        --activate) st_up_require_mode_legacy; MODE="activate" ;;
        --rollback) st_up_require_mode_legacy; MODE="rollback" ;;
        --reason=*) ROLLBACK_REASON="${1#*=}" ;;
        --restore-config) RESTORE_CONFIG=true ;;
        --force-no-emergency-backup) FORCE_NO_EMERGENCY_BACKUP=true ;;
        --yes) ASSUME_YES=true ;;
        --channel=*)
            CHANNEL_FLAG="${1#*=}"
            if [ "$CHANNEL_FLAG" != "stable" ] && [ "$CHANNEL_FLAG" != "beta" ]; then
                st_up_usage >&2; echo "ERROR: --channel must be \"stable\" or \"beta\" (got \"$CHANNEL_FLAG\")." >&2; exit 2
            fi
            ;;
        --history) st_up_require_mode_legacy; MODE="history" ;;
        --limit=*) HISTORY_LIMIT="${1#*=}"; LIMIT_EXPLICIT=true ;;
        --doctor) st_up_require_mode_legacy; MODE="doctor" ;;
        --override-maintenance-window) OVERRIDE_MAINTENANCE_WINDOW=true ;;
        --help|-h) MODE="help" ;;
        *)
            st_up_usage >&2
            echo "ERROR: Unknown argument: $1" >&2
            exit 2
            ;;
    esac
    shift
done

if [ "$MODE" = "help" ]; then
    st_up_usage
    exit 0
fi
if [ "$DRY_RUN" = true ] && [ "$MODE" != "prepare" ] && [ "$MODE" != "rollback" ]; then
    st_up_usage >&2
    echo "ERROR: --dry-run requires --latest, --target-version, or --rollback." >&2
    exit 2
fi
if [ "$ASSUME_YES" = true ] && [ "$MODE" != "prepare" ] && [ "$MODE" != "activate" ] && [ "$MODE" != "rollback" ]; then
    st_up_usage >&2
    echo "ERROR: --yes requires --latest, --target-version, --activate, or --rollback." >&2
    exit 2
fi

# Effective release channel: explicit --channel flag > Upgrade__Channel env var > default "stable".
# Resolved here (before the target-version validation below, which needs it) rather than at the
# very end of parsing, so every mode reads the same $EFFECTIVE_CHANNEL.
EFFECTIVE_CHANNEL="stable"
if [ -n "$CHANNEL_FLAG" ]; then
    EFFECTIVE_CHANNEL="$CHANNEL_FLAG"
elif [ -f "$SILVERTASK_ENV_FILE" ]; then
    env_channel="$(sed -n 's/^Upgrade__Channel=//p' "$SILVERTASK_ENV_FILE" | head -1)"
    if [ "$env_channel" = "stable" ] || [ "$env_channel" = "beta" ]; then
        EFFECTIVE_CHANNEL="$env_channel"
    fi
    unset env_channel
fi

if [ "$MODE" = "prepare" ] && [ "$TARGET_SELECTOR" = "target-version" ]; then
    target_version_valid=false
    if [ -n "$TARGET_VERSION" ]; then
        if st_up_semver_valid "$TARGET_VERSION"; then
            target_version_valid=true
        elif [ "$EFFECTIVE_CHANNEL" = "beta" ] && st_up_semver_valid_prerelease "$TARGET_VERSION"; then
            target_version_valid=true
        fi
    fi
    if [ "$target_version_valid" != true ]; then
        st_up_usage >&2
        if [ "$EFFECTIVE_CHANNEL" = "beta" ]; then
            echo "ERROR: --target-version \"$TARGET_VERSION\" is not a valid version (expected MAJOR.MINOR.PATCH or, on the beta channel, MAJOR.MINOR.PATCH-identifier, e.g. 1.1.0 or 1.1.0-beta)." >&2
        else
            echo "ERROR: --target-version \"$TARGET_VERSION\" is not a valid version (expected MAJOR.MINOR.PATCH, e.g. 1.1.0). Pre-release versions require --channel=beta." >&2
        fi
        exit 2
    fi
    unset target_version_valid
fi
if [ -n "$ROLLBACK_REASON" ] && [ "$MODE" != "rollback" ]; then
    st_up_usage >&2
    echo "ERROR: --reason requires --rollback." >&2
    exit 2
fi
if [ "$RESTORE_CONFIG" = true ] && [ "$MODE" != "rollback" ]; then
    st_up_usage >&2
    echo "ERROR: --restore-config requires --rollback." >&2
    exit 2
fi
if [ "$FORCE_NO_EMERGENCY_BACKUP" = true ] && [ "$MODE" != "rollback" ]; then
    st_up_usage >&2
    echo "ERROR: --force-no-emergency-backup requires --rollback." >&2
    exit 2
fi
if [ -n "$CHANNEL_FLAG" ] && [ "$MODE" != "check" ] && [ "$MODE" != "status" ] && [ "$MODE" != "prepare" ]; then
    st_up_usage >&2
    echo "ERROR: --channel requires --check, --status, --latest, or --target-version." >&2
    exit 2
fi
if [ "$LIMIT_EXPLICIT" = true ] && [ "$MODE" != "history" ]; then
    st_up_usage >&2
    echo "ERROR: --limit requires --history." >&2
    exit 2
fi
if [ "$MODE" = "history" ] && ! [[ "$HISTORY_LIMIT" =~ ^[0-9]+$ ]]; then
    st_up_usage >&2
    echo "ERROR: --limit must be a positive integer." >&2
    exit 2
fi
if [ "$OVERRIDE_MAINTENANCE_WINDOW" = true ] && [ "$MODE" != "activate" ] && [ "$MODE" != "rollback" ]; then
    st_up_usage >&2
    echo "ERROR: --override-maintenance-window requires --activate or --rollback." >&2
    exit 2
fi

st_require_root "$@"

# --- Upgrade-engine commands (Phase 52) ---
# Each of these prints its report/result and calls `exit` itself; MODE=legacy falls through to
# the unchanged Phase 51-and-earlier script body below instead.

cmd_check() {
    echo "Silver Task Update Check"
    echo ""
    local installed discovered latest
    installed="$(st_up_installed_version || echo unknown)"
    echo "Installed Version: $installed"
    echo "Release Channel: $EFFECTIVE_CHANNEL"

    if ! discovered="$(st_up_discover_releases "$EFFECTIVE_CHANNEL")"; then
        echo "Latest ${EFFECTIVE_CHANNEL^} Version: UNKNOWN"
        echo ""
        echo "Update Check: FAILED (could not reach $SILVERTASK_REPO_URL)"
        st_up_log "check: repository unreachable (installed=$installed, channel=$EFFECTIVE_CHANNEL)"
        exit 7
    fi
    if [ -z "$discovered" ]; then
        echo "Latest ${EFFECTIVE_CHANNEL^} Version: UNKNOWN"
        echo ""
        echo "Update Check: FAILED (no $EFFECTIVE_CHANNEL releases found on $SILVERTASK_REPO_URL)"
        st_up_log "check: no $EFFECTIVE_CHANNEL releases discovered (installed=$installed)"
        exit 7
    fi
    latest="$(printf '%s\n' "$discovered" | tail -1)"
    echo "Latest ${EFFECTIVE_CHANNEL^} Version: $latest"
    echo ""
    if [ "$installed" = "unknown" ]; then
        echo "Update Check: FAILED (installed version unknown — see README 'Version information')"
        st_up_log "check: installed version unknown (latest=$latest)"
        exit 3
    fi
    if [ "$(st_up_semver_compare "$installed" "$latest")" -lt 0 ]; then
        echo "UPDATE AVAILABLE"
        st_up_log "check: update available (installed=$installed latest=$latest channel=$EFFECTIVE_CHANNEL)"
    else
        echo "YOU ARE UP TO DATE"
        st_up_log "check: up to date (installed=$installed latest=$latest channel=$EFFECTIVE_CHANNEL)"
    fi
    exit 0
}

cmd_status() {
    echo "Silver Task Upgrade Status"
    echo ""
    st_up_version_consistency
    echo "Installed Version: ${ST_UP_INSTALLED:-UNKNOWN}"
    echo "Running Version: ${ST_UP_RUNNING:-UNKNOWN}"
    echo "Version Status: $ST_UP_CONSISTENCY"
    echo ""

    echo "Release Channel: $EFFECTIVE_CHANNEL"
    local discovered latest
    if discovered="$(st_up_discover_releases "$EFFECTIVE_CHANNEL")" && [ -n "$discovered" ]; then
        latest="$(printf '%s\n' "$discovered" | tail -1)"
        echo "Latest ${EFFECTIVE_CHANNEL^} Version: $latest"
        if [ -n "$ST_UP_INSTALLED" ]; then
            if [ "$(st_up_semver_compare "$ST_UP_INSTALLED" "$latest")" -lt 0 ]; then
                echo "Update Available: YES"
            else
                echo "Update Available: NO"
            fi
        else
            echo "Update Available: UNKNOWN"
        fi
    else
        echo "Latest ${EFFECTIVE_CHANNEL^} Version: UNKNOWN"
        echo "Update Check: FAILED"
    fi
    echo ""

    local lock_active=false
    if ! st_up_lock_probe; then
        lock_active=true
    fi

    declare -A state=()
    local state_output=""
    if state_output="$(st_up_state_read 2>/dev/null)"; then
        while IFS='=' read -r key value; do
            [ -n "$key" ] && state["$key"]="$value"
        done <<< "$state_output"
    fi
    declare -A rb_state=()
    local rb_state_output=""
    if rb_state_output="$(st_rb_state_read 2>/dev/null)"; then
        while IFS='=' read -r key value; do
            [ -n "$key" ] && rb_state["$key"]="$value"
        done <<< "$rb_state_output"
    fi
    st_up_maintenance_probe

    if [ "$lock_active" = true ]; then
        echo "Upgrade Status: IN PROGRESS"
        echo ""
        echo "Operation: ${ST_UP_LOCK_OPERATION_TYPE:-unknown}"
        echo "Target Version: ${ST_UP_LOCK_TARGET:-unknown}"
        echo "Started: ${ST_UP_LOCK_STARTED:-unknown}"
        if [ "$ST_UP_MAINTENANCE_ACTIVE" = true ]; then
            echo "Maintenance Mode: ACTIVE"
        fi
        exit 0
    fi

    # Phase 54/55 — a maintenance flag left active with NO process holding the upgrade lock is
    # always a red flag (reboot, power loss, kill -9 mid-operation — brief's own scenarios),
    # regardless of what upgrade-state.json's own status says. Checked before the state-based
    # cases below, and reported/exited distinctly (exit 25 for an interrupted activation, exit 34
    # for an interrupted rollback) since this is more urgent than a merely-stale prepare attempt:
    # the application may currently be unreachable. Distinguished by the maintenance flag's own
    # "upgradeId" field — a rollback always writes its rollback ID (prefixed "rollback-") into
    # that exact field (see st_up_maintenance_enable's call sites), so no new flag-file schema is
    # needed to tell the two apart.
    if [ "$ST_UP_MAINTENANCE_ACTIVE" = true ]; then
        case "$ST_UP_MAINTENANCE_UPGRADE_ID" in
            rollback-*)
                echo "Upgrade Status: INTERRUPTED ROLLBACK DETECTED"
                echo ""
                echo "Maintenance mode is still ACTIVE but no process currently holds the upgrade lock —"
                echo "a rollback was interrupted (server reboot, power loss, or the process was killed)."
                echo ""
                echo "  Rollback ID: ${ST_UP_MAINTENANCE_UPGRADE_ID:-unknown}"
                echo "  Related Upgrade ID: ${rb_state[relatedUpgradeId]:-unknown}"
                echo "  Last Step: ${rb_state[currentStep]:-unknown}"
                echo "  Current Release: ${rb_state[previousFailedVersion]:-unknown}"
                echo "  Target Release: ${ST_UP_MAINTENANCE_TARGET:-unknown}"
                echo "  Database Restore Status: ${rb_state[databaseRestorePerformed]:-unknown} (decision: ${rb_state[databaseRestoreDecision]:-unknown})"
                echo "  Maintenance Mode Status: ACTIVE since ${ST_UP_MAINTENANCE_STARTED:-unknown}"
                echo "  Emergency Backup: ${rb_state[emergencyBackupDir]:-none recorded}"
                echo ""
                echo "The application is likely returning 503 to normal traffic right now. This is NOT"
                echo "automatically resumed or cleaned up — investigate (systemctl status"
                echo "$SILVERTASK_SERVICE_NAME, journalctl -u $SILVERTASK_SERVICE_NAME, $SILVERTASK_UPGRADE_LOG_FILE)"
                echo "and resolve manually before retrying. See docs/rollback.md \"Interrupted rollback"
                echo "detection.\""
                exit 34
                ;;
            *)
                echo "Upgrade Status: INTERRUPTED UPGRADE DETECTED"
                echo ""
                echo "Maintenance mode is still ACTIVE but no process currently holds the upgrade lock —"
                echo "activation was interrupted (server reboot, power loss, or the process was killed)."
                echo ""
                echo "  Upgrade ID: ${ST_UP_MAINTENANCE_UPGRADE_ID:-unknown}"
                echo "  Previous Version: ${state[currentVersion]:-unknown}"
                echo "  Target Version: ${ST_UP_MAINTENANCE_TARGET:-unknown}"
                echo "  Last Recorded Step: ${state[currentStep]:-unknown}"
                echo "  Maintenance Mode Status: ACTIVE since ${ST_UP_MAINTENANCE_STARTED:-unknown}"
                echo "  Backup: ${state[backupDir]:-unknown}"
                echo ""
                echo "The application is likely returning 503 to normal traffic right now. This is NOT"
                echo "automatically resumed or cleaned up — investigate (systemctl status"
                echo "$SILVERTASK_SERVICE_NAME, journalctl -u $SILVERTASK_SERVICE_NAME, $SILVERTASK_UPGRADE_LOG_FILE)"
                echo "and resolve manually before retrying. See docs/upgrade-activation.md \"Interrupted"
                echo "upgrade detection.\" A rollback ('--rollback') may be the appropriate next step once"
                echo "the underlying issue is understood."
                exit 25
                ;;
        esac
    fi

    case "${state[status]:-}" in
        CHECKING|VALIDATING|PREPARING|UPGRADE_IN_PROGRESS|BACKING_UP|VERIFYING_BACKUP|CHECKING_PERSISTENT_DATA|PLANNING_MIGRATIONS|ACTIVATING|MAINTENANCE_ENABLED|MIGRATING|STARTING_SERVICES)
            echo "Upgrade Status: STALE UPGRADE LOCK DETECTED"
            echo ""
            echo "A previous upgrade attempt did not finish cleanly:"
            echo "  Upgrade ID: ${state[upgradeId]:-unknown}"
            echo "  Target Version: ${state[targetVersion]:-unknown}"
            echo "  Started: ${state[startTimeUtc]:-unknown}"
            echo "  Last step: ${state[currentStep]:-unknown}"
            echo ""
            echo "No process currently holds the upgrade lock, and maintenance mode is not active —"
            echo "it is safe to retry with --latest/--target-version (a prepare-stage interruption)"
            echo "or --activate (if this was already READY_FOR_ACTIVATION). Any pre-upgrade backup it"
            echo "managed to create before being interrupted is still on disk and was not deleted."
            ;;
        READY_FOR_ACTIVATION)
            echo "Upgrade Status: READY FOR ACTIVATION"
            echo ""
            echo "  Upgrade ID: ${state[upgradeId]:-unknown}"
            echo "  Target Version: ${state[targetVersion]:-unknown}"
            echo "  Prepared: ${state[lastUpdatedUtc]:-unknown}"
            echo "  Backup: ${state[backupStatus]:-unknown} (verification: ${state[backupVerificationStatus]:-unknown})"
            echo "  Persistent data check: ${state[persistentDataCheckStatus]:-unknown}"
            echo "  Migration validation: ${state[migrationValidationStatus]:-unknown}"
            echo "  Migration plan: ${state[migrationPlanStatus]:-unknown} (required: ${state[migrationRequired]:-unknown})"
            echo ""
            echo "A validated release and pre-upgrade backup are staged, but nothing has been"
            echo "activated — the running application is still on the installed version above."
            echo "Run 'sudo ./scripts/update-debian.sh --activate' to activate it."
            ;;
        COMPLETED)
            echo "Upgrade Status: COMPLETED"
            echo "Last Upgrade ID: ${state[upgradeId]:-unknown}"
            echo "Previous Version: ${state[currentVersion]:-unknown}"
            echo "Target Version: ${state[targetVersion]:-unknown}"
            echo "Started: ${state[startTimeUtc]:-unknown}"
            echo "Completed: ${state[completedAtUtc]:-unknown}"
            echo ""
            echo "Health Check: $([ "${state[healthCheckStatus]:-}" = OK ] && echo PASSED || echo "${state[healthCheckStatus]:-unknown}")"
            echo "Version Validation: $([ "${state[versionValidationStatus]:-}" = OK ] && echo PASSED || echo "${state[versionValidationStatus]:-unknown}")"
            echo "Smoke Tests: $([ "${state[smokeTestStatus]:-}" = OK ] && echo PASSED || echo "${state[smokeTestStatus]:-unknown}")"
            ;;
        FAILED)
            echo "Upgrade Status: FAILED"
            echo ""
            echo "  Upgrade ID: ${state[upgradeId]:-unknown}"
            echo "  Previous Version: ${state[currentVersion]:-unknown}"
            echo "  Target Version: ${state[targetVersion]:-unknown}"
            echo "  Failed during: ${state[currentStep]:-unknown}"
            echo "  Last updated: ${state[lastUpdatedUtc]:-unknown}"
            echo ""
            echo "The installed version was NOT changed. See $SILVERTASK_UPGRADE_LOG_FILE for the"
            echo "specific failure and docs/upgrade-activation.md \"Failure handling.\""
            ;;
        *)
            echo "Upgrade Status: IDLE"
            if [ -n "${state[status]:-}" ]; then
                echo ""
                echo "Last attempt: ${state[status]} (target ${state[targetVersion]:-unknown}, ${state[lastUpdatedUtc]:-unknown})"
            fi
            ;;
    esac

    # --- Phase 55 — rollback status, shown as its own section alongside (never instead of) the
    # upgrade status above, matching the brief's own "Last Upgrade: FAILED" + "Last Rollback:
    # COMPLETED" side-by-side example. Only printed once a rollback has ever actually run. ---
    if [ -n "${rb_state[status]:-}" ]; then
        echo ""
        case "${rb_state[status]:-}" in
            ROLLBACK_REQUESTED|ROLLBACK_VALIDATING|ROLLBACK_PREPARING|ROLLBACK_MAINTENANCE|ROLLBACK_APPLICATION|ROLLBACK_DATABASE|ROLLBACK_CONFIGURATION|ROLLBACK_SERVICES|ROLLBACK_HEALTH_VALIDATION)
                echo "Last Rollback: STALE ROLLBACK DETECTED"
                echo ""
                echo "A previous rollback attempt did not finish cleanly (no process holds the upgrade"
                echo "lock, and maintenance mode is not active — an interruption before maintenance mode"
                echo "was ever enabled):"
                echo "  Rollback ID: ${rb_state[rollbackId]:-unknown}"
                echo "  Related Upgrade ID: ${rb_state[relatedUpgradeId]:-unknown}"
                echo "  Rollback Target: ${rb_state[restoredVersion]:-unknown}"
                echo "  Last step: ${rb_state[currentStep]:-unknown}"
                echo ""
                echo "Safe to retry with --rollback. Any emergency backup it managed to create before"
                echo "being interrupted is still on disk and was not deleted."
                ;;
            ROLLBACK_COMPLETED)
                echo "Last Rollback: COMPLETED"
                echo "Rollback ID: ${rb_state[rollbackId]:-unknown}"
                echo "Related Upgrade: ${rb_state[relatedUpgradeId]:-unknown}"
                echo "Rollback Target: ${rb_state[restoredVersion]:-unknown}"
                echo "Rolled back from: ${rb_state[previousFailedVersion]:-unknown}"
                echo "Reason: ${rb_state[reason]:-unknown}"
                echo "Started: ${rb_state[startTimeUtc]:-unknown}"
                echo "Completed: ${rb_state[completedAtUtc]:-unknown}"
                echo ""
                echo "Database Restored: $([ "${rb_state[databaseRestorePerformed]:-}" = true ] && echo YES || echo NO) (decision: ${rb_state[databaseRestoreDecision]:-unknown})"
                echo "Configuration Restored: $([ "${rb_state[configurationRestorePerformed]:-}" = true ] && echo YES || echo NO)"
                echo "Application Health: $([ "${rb_state[healthCheckStatus]:-}" = OK ] && echo PASSED || echo "${rb_state[healthCheckStatus]:-unknown}")"
                ;;
            ROLLBACK_FAILED)
                echo "Last Rollback: FAILED"
                echo ""
                echo "  Rollback ID: ${rb_state[rollbackId]:-unknown}"
                echo "  Related Upgrade: ${rb_state[relatedUpgradeId]:-unknown}"
                echo "  Rollback Target: ${rb_state[restoredVersion]:-unknown}"
                echo "  Failed during: ${rb_state[currentStep]:-unknown}"
                echo "  Last updated: ${rb_state[lastUpdatedUtc]:-unknown}"
                echo ""
                echo "See $SILVERTASK_UPGRADE_LOG_FILE for the specific failure and docs/rollback.md"
                echo "\"Failed rollback handling.\""
                ;;
        esac
    fi
    exit 0
}

cmd_prepare() {
    local mode_label
    if [ "$TARGET_SELECTOR" = "latest" ]; then mode_label="--latest"; else mode_label="--target-version $TARGET_VERSION"; fi
    st_up_log "prepare: starting ($mode_label, dry-run=$DRY_RUN)"

    st_up_version_consistency
    if [ "$ST_UP_CONSISTENCY" = "MISMATCH" ]; then
        st_error "VERSION MISMATCH DETECTED — installed=\"$ST_UP_INSTALLED\" running=\"$ST_UP_RUNNING\"."
        st_error "The upgrade engine will not act on an inconsistent installation. Resolve the mismatch (see README 'Version information') before retrying."
        st_up_log "prepare: aborted, version mismatch (installed=$ST_UP_INSTALLED running=$ST_UP_RUNNING)"
        exit 3
    fi
    if [ "$ST_UP_CONSISTENCY" = "UNKNOWN" ]; then
        st_error "Could not determine installed and/or running version — installed=\"${ST_UP_INSTALLED:-unknown}\" running=\"${ST_UP_RUNNING:-unreachable}\"."
        st_error "The upgrade engine requires a known, consistent installation before preparing an upgrade."
        st_up_log "prepare: aborted, version unknown"
        exit 3
    fi
    local installed="$ST_UP_INSTALLED"
    st_info "Installed Version: $installed"

    st_step "Discovering $EFFECTIVE_CHANNEL releases"
    local discovered
    if ! discovered="$(st_up_discover_releases "$EFFECTIVE_CHANNEL")"; then
        st_error "Could not reach $SILVERTASK_REPO_URL to discover releases."
        st_up_log "prepare: aborted, repository unreachable"
        exit 7
    fi
    if [ -z "$discovered" ]; then
        st_error "No $EFFECTIVE_CHANNEL releases found on $SILVERTASK_REPO_URL."
        st_up_log "prepare: aborted, no $EFFECTIVE_CHANNEL releases discovered"
        exit 7
    fi

    local target
    if [ "$TARGET_SELECTOR" = "latest" ]; then
        target="$(printf '%s\n' "$discovered" | tail -1)"
        st_info "Latest $EFFECTIVE_CHANNEL release: $target"
    else
        target="$TARGET_VERSION"
        if ! printf '%s\n' "$discovered" | grep -qxF "$target"; then
            st_error "Target version \"$target\" is not an available $EFFECTIVE_CHANNEL release."
            st_error "Available $EFFECTIVE_CHANNEL releases: $(printf '%s\n' "$discovered" | tr '\n' ' ')"
            st_up_log "prepare: aborted, target $target unavailable on channel $EFFECTIVE_CHANNEL"
            exit 4
        fi
    fi
    st_info "Target Version: $target"

    if [ "$target" = "$installed" ]; then
        st_info "ALREADY RUNNING TARGET VERSION"
        st_up_log "prepare: no-op, already on $target"
        exit 0
    fi
    # Ordering compares only the MAJOR.MINOR.PATCH part — a pre-release tag on the beta channel
    # (e.g. 1.1.0-beta) is ordered exactly like its base version 1.1.0 for this purpose; comparing
    # pre-release identifiers against each other is out of scope for this engine.
    if [ "$(st_up_semver_compare "${target%%-*}" "${installed%%-*}")" -lt 0 ]; then
        st_error "DOWNGRADE NOT SUPPORTED BY UPGRADE ENGINE (installed=$installed, requested=$target)."
        st_error "Downgrades are handled separately through a future rollback/recovery system."
        st_up_log "prepare: aborted, downgrade requested ($installed -> $target)"
        exit 5
    fi
    st_info "Upgrade path: $installed -> $target"

    st_step "Checking release metadata"
    local metadata_content="" meta_rc=0
    if [ "$DRY_RUN" = true ]; then
        # Dry-run never writes to the local git object database (no `fetch`) — it only inspects
        # a tag's metadata if it already happens to be available locally from an earlier run.
        if git -C "$SILVERTASK_SOURCE_DIR" rev-parse -q --verify "refs/tags/v$target" >/dev/null 2>&1; then
            metadata_content="$(st_up_read_release_metadata "$SILVERTASK_SOURCE_DIR" "$target")"
            st_up_metadata_validate "$target" "$metadata_content" "$installed" || meta_rc=$?
            st_info "Release metadata source: $ST_UP_META_SOURCE"
        else
            st_info "Release metadata source: unverified (tag v$target not yet fetched locally; --dry-run does not fetch)"
        fi
    else
        git -C "$SILVERTASK_SOURCE_DIR" fetch --tags >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1 \
            || { st_error "Could not fetch tags from $SILVERTASK_REPO_URL."; st_up_log "prepare: aborted, tag fetch failed"; exit 7; }
        metadata_content="$(st_up_read_release_metadata "$SILVERTASK_SOURCE_DIR" "$target")"
        st_up_metadata_validate "$target" "$metadata_content" "$installed" || meta_rc=$?
        st_info "Release metadata source: $ST_UP_META_SOURCE"
    fi
    if [ "$meta_rc" -eq 1 ]; then
        st_up_log "prepare: aborted, malformed release metadata for $target"
        exit 1
    elif [ "$meta_rc" -eq 2 ]; then
        st_up_log "prepare: aborted, $target requires minimum version $ST_UP_META_MIN_VERSION (installed $installed)"
        exit 5
    fi
    # Cross-check the release's own declared metadata channel against the effective operating
    # channel — metadata validation above only confirmed "beta" is a well-formed value, not that
    # this operator is allowed to install one. A beta-declared release can never be selected while
    # operating on the stable channel, even via --target-version with a non-prerelease-looking tag.
    if [ -n "$metadata_content" ]; then
        local declared_channel="${ST_UP_META_CHANNEL:-stable}"
        st_info "Release channel (declared): $declared_channel"
        if [ "$declared_channel" = "beta" ] && [ "$EFFECTIVE_CHANNEL" != "beta" ]; then
            st_error "RELEASE CHANNEL MISMATCH — v$target is a beta-channel release; the effective channel is \"$EFFECTIVE_CHANNEL\"."
            st_error "Pass --channel=beta (or set Upgrade__Channel=beta in $SILVERTASK_ENV_FILE) to opt in, or choose a stable release instead."
            st_up_log "prepare: aborted, channel mismatch (declared=$declared_channel effective=$EFFECTIVE_CHANNEL, target=$target)"
            exit 37
        fi
    fi
    st_info "Requires database migration: $ST_UP_META_REQUIRES_DB_MIGRATION | Requires data migration: $ST_UP_META_REQUIRES_DATA_MIGRATION | Requires restart: $ST_UP_META_REQUIRES_RESTART"

    st_step "Checking disk space"
    if st_up_disk_space_check; then
        st_info "Disk space check passed: ${ST_UP_DISK_AVAILABLE_MB}MB available, ~${ST_UP_DISK_REQUIRED_MB}MB estimated needed."
    else
        st_error "UPGRADE BLOCKED — INSUFFICIENT DISK SPACE (${ST_UP_DISK_AVAILABLE_MB}MB available, ~${ST_UP_DISK_REQUIRED_MB}MB estimated needed)."
        st_up_log "prepare: aborted, insufficient disk space"
        exit 8
    fi

    if [ "$DRY_RUN" = true ]; then
        echo ""
        echo "DRY RUN — no changes made."
        echo "Installed Version: $installed"
        echo "Target Version: $target"
        echo "Upgrade path: supported (forward upgrade)"
        echo "Release availability: confirmed on $SILVERTASK_REPO_URL"
        echo "Release metadata: $ST_UP_META_SOURCE"
        echo "Disk space (release staging): ${ST_UP_DISK_AVAILABLE_MB}MB available, ~${ST_UP_DISK_REQUIRED_MB}MB estimated needed"
        echo ""
        echo "Persistent Data Check:"
        if st_up_persistent_data_check "$SILVERTASK_ENV_FILE"; then
            echo "  OK — attachment storage ($ST_UP_PERSISTENT_DATA_STORAGE_ROOT) is outside the application install tree."
        else
            echo "  UPGRADE WOULD BE BLOCKED — PERSISTENT DATA LOCATION UNSAFE"
            echo "  $ST_UP_PERSISTENT_DATA_ISSUE"
        fi
        echo ""
        echo "Backup Plan (not executed in dry-run):"
        st_up_backup_disk_space_check || true
        echo "  Would create: database dump (pg_dump -F c) + attachments archive + configuration copy"
        echo "  Would write to: $SILVERTASK_BACKUP_DIR/<timestamp>/ (tagged pre-upgrade, linked to a new upgrade ID)"
        echo "  Disk space for backup: ${ST_UP_BACKUP_DISK_AVAILABLE_MB}MB available, ~${ST_UP_BACKUP_DISK_REQUIRED_MB}MB estimated needed"
        echo ""
        echo "Migration Plan: not available in dry-run (requires staging the release — run without"
        echo "  --dry-run, or a real --latest/--target-version prepare, to generate the concrete SQL plan)."
        echo ""
        echo "Maintenance Mode Plan: a subsequent 'sudo ./scripts/update-debian.sh --activate' would"
        echo "  enable maintenance mode (503 for all but /api/health*) before touching anything live."
        echo "Activation Plan: build v$target into a fresh directory, then (only if that succeeds)"
        echo "  swap it in for $SILVERTASK_PUBLISH_DIR — the previous release is kept, never deleted."
        echo "Service Restart Plan: 'systemctl stop silvertask' before the swap, 'systemctl start"
        echo "  silvertask' after migrations — no other service is touched (database, nginx untouched)."
        echo "Health Check Plan: poll $SILVERTASK_PUBLISH_DIR's /api/health/ready with bounded retries;"
        echo "  timeout fails the activation and leaves maintenance mode on for manual investigation."
        echo "Version Commit Plan: installed-version.json is only rewritten after health AND version"
        echo "  validation both pass — never before."
        echo ""
        echo "None of the above runs until a separate, explicitly confirmed 'sudo ./scripts/update-debian.sh"
        echo "--activate' — --latest/--target-version (even without --dry-run) only prepares."
        echo ""
        echo "Preparation plan: fetch tag v$target, stage into an isolated git worktree under"
        echo "  $SILVERTASK_UPGRADE_STAGING_DIR/$target, create + verify a pre-upgrade backup, validate"
        echo "  migrations, record upgrade lock/state/log — the running application, database, and"
        echo "  installed version would NOT be touched."
        st_up_log "prepare: dry-run complete ($installed -> $target)"
        exit 0
    fi

    echo ""
    echo "Installed Version: $installed"
    echo "Target Version: $target"
    echo ""
    echo "This operation will prepare the upgrade (fetch and validate the release into an isolated"
    echo "location; it will NOT activate it — the running application, database, and installed"
    echo "version are not touched)."
    echo ""
    if [ "$ASSUME_YES" != true ]; then
        local reply=""
        read -r -p "Continue? [y/N]: " reply || true
        case "$reply" in
            y|Y) ;;
            *)
                st_info "Aborted by administrator."
                st_up_log "prepare: aborted by administrator ($installed -> $target)"
                exit 0
                ;;
        esac
    fi

    st_step "Acquiring upgrade lock"
    if ! st_up_lock_acquire "$target"; then
        st_up_lock_probe || true
        st_error "UPGRADE ALREADY IN PROGRESS (operation: ${ST_UP_LOCK_OPERATION_TYPE:-unknown}, target ${ST_UP_LOCK_TARGET:-unknown}, started ${ST_UP_LOCK_STARTED:-unknown}, pid ${ST_UP_LOCK_PID:-unknown})."
        st_up_log "prepare: aborted, lock held by pid ${ST_UP_LOCK_PID:-unknown}"
        exit 6
    fi
    # flock already releases automatically if this process dies; the trap makes a clean exit
    # (success or a handled failure below) release it immediately rather than waiting on process
    # teardown.
    trap 'st_up_lock_release' EXIT

    local upgrade_id start_time
    upgrade_id="$(st_up_generate_upgrade_id)"
    start_time="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
    st_info "Upgrade ID: $upgrade_id"
    ST_UP_STATE_BACKUP_STATUS="PENDING"
    ST_UP_STATE_BACKUP_VERIFICATION_STATUS="PENDING"
    ST_UP_STATE_PERSISTENT_DATA_STATUS="PENDING"
    ST_UP_STATE_MIGRATION_VALIDATION_STATUS="PENDING"
    ST_UP_STATE_MIGRATION_PLAN_STATUS="PENDING"
    st_up_state_write "PREPARING" "$installed" "$target" "staging release" "$start_time" "$upgrade_id"
    st_up_log "prepare: lock acquired, upgrade $upgrade_id, staging $target"

    st_step "Fetching and staging release $target"
    if ! st_up_prepare_worktree "$SILVERTASK_SOURCE_DIR" "$target" "$SILVERTASK_UPGRADE_STAGING_DIR"; then
        st_up_state_write "FAILED" "$installed" "$target" "staging release" "$start_time" "$upgrade_id"
        st_up_log "prepare: FAILED, could not stage worktree for $target"
        st_fail "Could not fetch/stage release $target." "Check $SILVERTASK_UPGRADE_LOG_FILE and that $SILVERTASK_REPO_URL is reachable."
    fi
    st_info "Staged at $ST_UP_STAGED_DIR."

    st_step "Verifying staged release"
    if ! "$SCRIPT_DIR/check-version.sh" "$ST_UP_STAGED_DIR" > /dev/null; then
        st_up_cleanup_worktree "$SILVERTASK_SOURCE_DIR" "$ST_UP_STAGED_DIR"
        st_up_state_write "FAILED" "$installed" "$target" "verifying staged release" "$start_time" "$upgrade_id"
        st_up_log "prepare: FAILED, staged release $target failed check-version.sh"
        st_fail "Staged release $target failed its own version/tag consistency check." "See $SILVERTASK_UPGRADE_LOG_FILE."
    fi
    st_info "[OK] Version consistency validated"
    st_info "[OK] Target release validated"
    st_info "[OK] Upgrade lock acquired"

    # --- Phase 53: safety layer — backups, persistent-data protection, migration orchestration.
    # Nothing below this point ever touches the running application, the database, or
    # installed-version.json; every failure cleans up the staged worktree and records FAILED. ---

    st_step "Checking persistent data locations"
    st_up_state_write "CHECKING_PERSISTENT_DATA" "$installed" "$target" "checking persistent data" "$start_time" "$upgrade_id"
    if ! st_up_persistent_data_check "$SILVERTASK_ENV_FILE"; then
        ST_UP_STATE_PERSISTENT_DATA_STATUS="FAILED"
        st_up_cleanup_worktree "$SILVERTASK_SOURCE_DIR" "$ST_UP_STAGED_DIR"
        st_up_state_write "FAILED" "$installed" "$target" "checking persistent data" "$start_time" "$upgrade_id"
        st_error "UPGRADE BLOCKED — PERSISTENT DATA LOCATION UNSAFE"
        st_error "$ST_UP_PERSISTENT_DATA_ISSUE"
        st_up_log "prepare: FAILED, unsafe persistent data location: $ST_UP_PERSISTENT_DATA_ISSUE"
        exit 13
    fi
    ST_UP_STATE_PERSISTENT_DATA_STATUS="OK"
    st_info "[OK] Persistent data locations validated (attachments: $ST_UP_PERSISTENT_DATA_STORAGE_ROOT)"

    st_step "Checking disk space for backup"
    if ! st_up_backup_disk_space_check; then
        st_up_cleanup_worktree "$SILVERTASK_SOURCE_DIR" "$ST_UP_STAGED_DIR"
        st_up_state_write "FAILED" "$installed" "$target" "checking disk space for backup" "$start_time" "$upgrade_id"
        st_error "UPGRADE BLOCKED — INSUFFICIENT DISK SPACE (${ST_UP_BACKUP_DISK_AVAILABLE_MB}MB available, ~${ST_UP_BACKUP_DISK_REQUIRED_MB}MB estimated needed for backup)."
        st_up_log "prepare: FAILED, insufficient disk space for backup"
        exit 8
    fi
    st_info "[OK] Disk space for backup validated (${ST_UP_BACKUP_DISK_AVAILABLE_MB}MB available)"

    st_step "Creating pre-upgrade backup (database + attachments + configuration)"
    ST_UP_STATE_BACKUP_STATUS="IN_PROGRESS"
    st_up_state_write "BACKING_UP" "$installed" "$target" "creating pre-upgrade backup" "$start_time" "$upgrade_id"
    if ! st_up_run_backup "$SCRIPT_DIR" "pre-upgrade" "$upgrade_id" "$installed" "$target"; then
        st_up_cleanup_worktree "$SILVERTASK_SOURCE_DIR" "$ST_UP_STAGED_DIR"
        case "$ST_UP_BACKUP_RESULT" in
            DATABASE_FAILED)
                ST_UP_STATE_BACKUP_STATUS="FAILED"
                st_up_state_write "FAILED" "$installed" "$target" "database backup" "$start_time" "$upgrade_id"
                st_error "UPGRADE BLOCKED"; st_error "DATABASE BACKUP FAILED"
                st_up_log "prepare: FAILED, database backup failed (backup dir: ${ST_UP_BACKUP_DIR:-unknown})"
                exit 9 ;;
            DATABASE_UNVERIFIED)
                ST_UP_STATE_BACKUP_STATUS="OK"; ST_UP_STATE_BACKUP_VERIFICATION_STATUS="FAILED"
                st_up_state_write "FAILED" "$installed" "$target" "database backup verification" "$start_time" "$upgrade_id"
                st_error "UPGRADE BLOCKED"; st_error "DATABASE BACKUP VERIFICATION FAILED"
                st_up_log "prepare: FAILED, database backup verification failed (backup dir: ${ST_UP_BACKUP_DIR:-unknown})"
                exit 10 ;;
            CONFIG_FAILED)
                ST_UP_STATE_BACKUP_STATUS="OK"; ST_UP_STATE_BACKUP_VERIFICATION_STATUS="OK"
                st_up_state_write "FAILED" "$installed" "$target" "configuration backup" "$start_time" "$upgrade_id"
                st_error "UPGRADE BLOCKED"; st_error "CONFIGURATION BACKUP FAILED"
                st_up_log "prepare: FAILED, configuration backup failed (backup dir: ${ST_UP_BACKUP_DIR:-unknown})"
                exit 11 ;;
            CONFIG_UNVERIFIED)
                ST_UP_STATE_BACKUP_STATUS="OK"; ST_UP_STATE_BACKUP_VERIFICATION_STATUS="FAILED"
                st_up_state_write "FAILED" "$installed" "$target" "configuration backup verification" "$start_time" "$upgrade_id"
                st_error "UPGRADE BLOCKED"; st_error "CONFIGURATION BACKUP VERIFICATION FAILED"
                st_up_log "prepare: FAILED, configuration backup verification failed (backup dir: ${ST_UP_BACKUP_DIR:-unknown})"
                exit 12 ;;
            *)
                ST_UP_STATE_BACKUP_STATUS="FAILED"
                st_up_state_write "FAILED" "$installed" "$target" "backup" "$start_time" "$upgrade_id"
                st_error "UPGRADE BLOCKED"; st_error "DATABASE BACKUP FAILED"
                st_up_log "prepare: FAILED, backup-debian.sh failed before writing a manifest"
                exit 9 ;;
        esac
    fi
    ST_UP_STATE_BACKUP_STATUS="OK"; ST_UP_STATE_BACKUP_VERIFICATION_STATUS="OK"
    ST_UP_STATE_BACKUP_DIR="$ST_UP_BACKUP_DIR"
    st_info "[OK] Database backup created"
    st_info "[OK] Database backup verified"
    st_info "[OK] Configuration backup created"
    st_info "[OK] Configuration backup verified"
    st_info "Backup location: $ST_UP_BACKUP_DIR"

    st_step "Validating current database migration state"
    st_up_state_write "PLANNING_MIGRATIONS" "$installed" "$target" "validating current migration state" "$start_time" "$upgrade_id"
    if ! st_up_migration_current_state "$SILVERTASK_SOURCE_DIR" "$SILVERTASK_ENV_FILE"; then
        ST_UP_STATE_MIGRATION_VALIDATION_STATUS="FAILED"
        st_up_cleanup_worktree "$SILVERTASK_SOURCE_DIR" "$ST_UP_STAGED_DIR"
        st_up_state_write "FAILED" "$installed" "$target" "validating current migration state" "$start_time" "$upgrade_id"
        st_error "UPGRADE BLOCKED — DATABASE MIGRATION STATE INVALID"
        st_error "Could not confirm the currently installed code's migrations are fully, cleanly applied — see $SILVERTASK_UPGRADE_LOG_FILE."
        st_up_log "prepare: FAILED, current migration state invalid"
        exit 14
    fi

    st_step "Validating target release migrations"
    if ! st_up_migration_target_list "$ST_UP_STAGED_DIR"; then
        ST_UP_STATE_MIGRATION_VALIDATION_STATUS="FAILED"
        st_up_cleanup_worktree "$SILVERTASK_SOURCE_DIR" "$ST_UP_STAGED_DIR"
        st_up_state_write "FAILED" "$installed" "$target" "validating target migrations" "$start_time" "$upgrade_id"
        st_error "UPGRADE BLOCKED — target migration validation failed (malformed, unbuildable, or duplicate migrations)."
        st_error "See $SILVERTASK_UPGRADE_LOG_FILE."
        st_up_log "prepare: FAILED, target migration validation failed"
        exit 15
    fi
    ST_UP_STATE_MIGRATION_VALIDATION_STATUS="OK"
    local target_migration_count
    target_migration_count="$(printf '%s\n' "$ST_UP_MIGRATIONS_TARGET" | grep -c .)"
    st_info "[OK] Migration state validated ($target_migration_count migrations in target release)"

    st_step "Generating migration plan"
    st_up_migration_plan "$ST_UP_MIGRATIONS_APPLIED" "$ST_UP_MIGRATIONS_TARGET" "$ST_UP_META_REQUIRES_DATA_MIGRATION"
    local migration_script="$ST_UP_STAGED_DIR/migration-plan.sql"
    if ! st_up_migration_generate_script "$ST_UP_STAGED_DIR" "$migration_script"; then
        ST_UP_STATE_MIGRATION_PLAN_STATUS="FAILED"
        st_up_cleanup_worktree "$SILVERTASK_SOURCE_DIR" "$ST_UP_STAGED_DIR"
        st_up_state_write "FAILED" "$installed" "$target" "generating migration plan" "$start_time" "$upgrade_id"
        st_error "UPGRADE BLOCKED — migration planning failed (could not generate the migration script)."
        st_error "See $SILVERTASK_UPGRADE_LOG_FILE."
        st_up_log "prepare: FAILED, migration planning failed"
        exit 16
    fi
    ST_UP_STATE_MIGRATION_PLAN_STATUS="OK"
    if [ "$ST_UP_MIGRATION_PENDING_COUNT" -gt 0 ] || [ "$ST_UP_META_REQUIRES_DATA_MIGRATION" = "true" ]; then
        ST_UP_STATE_MIGRATION_REQUIRED="true"
    else
        ST_UP_STATE_MIGRATION_REQUIRED="false"
    fi
    st_info "[OK] Migration plan generated"
    st_info ""
    st_info "Migration Plan"
    st_info "  Current Application Version: $installed"
    st_info "  Target Application Version: $target"
    if [ "$ST_UP_MIGRATION_PENDING_COUNT" -gt 0 ]; then
        st_info "  Database Migration Required: YES"
    else
        st_info "  Database Migration Required: NO"
    fi
    if [ "$ST_UP_META_REQUIRES_DATA_MIGRATION" = "true" ]; then
        st_info "  Data Migration Required: YES"
    else
        st_info "  Data Migration Required: NO"
    fi
    st_info "  Classification: $ST_UP_MIGRATION_CLASSIFICATION"
    if [ "$ST_UP_MIGRATION_PENDING_COUNT" -gt 0 ]; then
        st_info "  Migration Steps:"
        local step_number=1 migration_name
        while IFS= read -r migration_name; do
            [ -n "$migration_name" ] || continue
            st_info "    $step_number. Apply migration $migration_name"
            step_number=$((step_number + 1))
        done <<< "$ST_UP_MIGRATION_PENDING"
    fi
    st_info "  Migration script (generated, not executed): $migration_script"

    st_up_state_write "READY_FOR_ACTIVATION" "$installed" "$target" "ready for activation" "$start_time" "$upgrade_id"
    st_up_log "prepare: READY_FOR_ACTIVATION, upgrade $upgrade_id ($installed -> $target), backup at $ST_UP_BACKUP_DIR, staged at $ST_UP_STAGED_DIR"
    st_info "=================================================================="
    st_info " READY FOR ACTIVATION"
    st_info " Upgrade ID: $upgrade_id"
    st_info " $installed -> $target staged at $ST_UP_STAGED_DIR"
    st_info " Pre-upgrade backup: $ST_UP_BACKUP_DIR"
    st_info " The running application is still on $installed — nothing has been activated."
    st_info " To activate this prepared release:"
    st_info "   sudo ./scripts/update-debian.sh --activate"
    st_info "=================================================================="
    exit 0
}

cmd_activate() {
    st_up_log "activate: starting (yes=$ASSUME_YES)"

    if ! st_up_activation_prerequisites_check; then
        st_error "UPGRADE ACTIVATION BLOCKED"
        st_error "$ST_UP_ACTIVATION_BLOCKED_REASON"
        st_up_log "activate: BLOCKED, $ST_UP_ACTIVATION_BLOCKED_REASON"
        exit 17
    fi
    local upgrade_id="$ST_UP_ACTIVATE_UPGRADE_ID"
    local previous_version="$ST_UP_ACTIVATE_CURRENT_VERSION"
    local target="$ST_UP_ACTIVATE_TARGET_VERSION"
    local backup_dir="$ST_UP_ACTIVATE_BACKUP_DIR"
    local migration_required="$ST_UP_ACTIVATE_MIGRATION_REQUIRED"

    echo "Silver Task Upgrade Activation"
    echo ""
    echo "Current Version: $previous_version"
    echo "Target Version: $target"
    echo "Upgrade ID: $upgrade_id"
    echo ""
    echo "Database Backup: VERIFIED"
    echo "Configuration Backup: VERIFIED"
    if [ "$migration_required" = "true" ]; then
        echo "Migration Required: YES"
    else
        echo "Migration Required: NO"
    fi
    echo ""
    echo "The application will enter maintenance mode during activation."
    echo ""
    if [ "$ASSUME_YES" != true ]; then
        local reply=""
        read -r -p "Continue? [y/N]: " reply || true
        case "$reply" in
            y|Y) ;;
            *)
                st_info "Aborted by administrator."
                st_up_log "activate: aborted by administrator ($previous_version -> $target)"
                exit 0
                ;;
        esac
    fi

    st_step "Checking maintenance-window policy"
    if ! st_up_maintenance_window_check "$SILVERTASK_ENV_FILE"; then
        if [ "$OVERRIDE_MAINTENANCE_WINDOW" = true ]; then
            echo ""
            echo "WARNING: the current time is outside the configured maintenance window"
            echo "($ST_UP_MAINTENANCE_WINDOW) — --override-maintenance-window was passed."
            if ! st_confirm_destructive "About to activate outside the configured maintenance window." "$target"; then
                st_info "Aborted by administrator (maintenance-window override not confirmed)."
                st_up_log "activate: aborted, maintenance-window override not confirmed"
                exit 0
            fi
            st_warn "Proceeding outside the maintenance window ($ST_UP_MAINTENANCE_WINDOW) — override confirmed."
        else
            st_error "UPGRADE BLOCKED BY MAINTENANCE WINDOW POLICY"
            st_error "Upgrade__MaintenanceWindow=$ST_UP_MAINTENANCE_WINDOW is configured and the current time is outside it. Pass --override-maintenance-window to proceed anyway (requires confirmation)."
            st_up_log "activate: BLOCKED, outside maintenance window $ST_UP_MAINTENANCE_WINDOW"
            exit 35
        fi
    elif [ "$ST_UP_MAINTENANCE_WINDOW_CONFIGURED" = true ]; then
        st_info "[OK] Inside configured maintenance window ($ST_UP_MAINTENANCE_WINDOW)"
    fi

    st_step "Acquiring upgrade lock"
    if ! st_up_lock_acquire "$target"; then
        st_up_lock_probe || true
        st_error "UPGRADE ALREADY IN PROGRESS (operation: ${ST_UP_LOCK_OPERATION_TYPE:-unknown}, target ${ST_UP_LOCK_TARGET:-unknown}, started ${ST_UP_LOCK_STARTED:-unknown}, pid ${ST_UP_LOCK_PID:-unknown})."
        st_up_log "activate: aborted, lock held by pid ${ST_UP_LOCK_PID:-unknown}"
        exit 6
    fi
    # Same crash-safety guarantee as cmd_prepare's lock: flock auto-releases if this process dies;
    # the trap makes a clean exit (success or a handled failure below) release the lock and record
    # this attempt in the durable release history — one trap covers every failure branch below
    # (build/maintenance/service/migration/health/version/smoke-test) instead of a call at each one.
    st_activate_finalize() {
        local rc=$?
        st_up_lock_release
        if [ "$rc" -eq 0 ]; then
            st_up_history_append "upgrade" "$upgrade_id" "$previous_version" "$target" "COMPLETED" ""
        else
            st_up_history_append "upgrade" "$upgrade_id" "$previous_version" "$target" "FAILED" "exit code $rc"
        fi
    }
    trap 'st_activate_finalize' EXIT

    local start_time
    start_time="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
    local -a timeline=("$start_time Activation started")

    ST_UP_STATE_BACKUP_STATUS="OK"; ST_UP_STATE_BACKUP_VERIFICATION_STATUS="OK"
    ST_UP_STATE_PERSISTENT_DATA_STATUS="OK"; ST_UP_STATE_MIGRATION_VALIDATION_STATUS="OK"
    ST_UP_STATE_MIGRATION_PLAN_STATUS="OK"; ST_UP_STATE_BACKUP_DIR="$backup_dir"
    ST_UP_STATE_MIGRATION_REQUIRED="$migration_required"
    ST_UP_STATE_ACTIVATION_STATUS="PENDING"; ST_UP_STATE_MAINTENANCE_STATUS="PENDING"
    ST_UP_STATE_MIGRATION_EXECUTION_STATUS="PENDING"; ST_UP_STATE_SERVICE_STATUS="PENDING"
    ST_UP_STATE_HEALTH_CHECK_STATUS="PENDING"; ST_UP_STATE_VERSION_VALIDATION_STATUS="PENDING"
    ST_UP_STATE_SMOKE_TEST_STATUS="PENDING"
    st_up_state_write "ACTIVATING" "$previous_version" "$target" "checking out and building target release" "$start_time" "$upgrade_id"
    st_up_log "activate: lock acquired, upgrade $upgrade_id, activating $previous_version -> $target"

    # Printed on any failure path below — brief's "failure recovery information," never claiming
    # the previous application is active again unless that's actually been verified (it isn't,
    # automatically, anywhere in this function — Phase 54 does not implement auto-recovery).
    print_recovery_info() {
        local failed_step="$1" extra="${2:-}"
        echo ""
        echo "Upgrade failed during: $failed_step"
        echo ""
        echo "Previous Version: $previous_version"
        echo "Target Version: $target"
        echo "Upgrade ID: $upgrade_id"
        echo ""
        echo "Database Backup: VERIFIED ($backup_dir)"
        echo "Configuration Backup: VERIFIED ($backup_dir)"
        echo ""
        echo "The system has NOT marked version $target as installed."
        [ -n "$extra" ] && echo "$extra"
        echo "See $SILVERTASK_UPGRADE_LOG_FILE for details."
    }

    # --- Build the target release into a FRESH directory before touching anything live. A
    # failure here leaves the old application completely untouched and still running. ---
    st_step "Building target release $target"
    local new_publish_dir="${SILVERTASK_PUBLISH_DIR}.new"
    rm -rf "$new_publish_dir"
    if ! (
        git -C "$SILVERTASK_SOURCE_DIR" fetch --tags
        git -C "$SILVERTASK_SOURCE_DIR" checkout "v$target"
        cd "$SILVERTASK_SOURCE_DIR"
        dotnet tool restore
        dotnet publish Silver-Task.Server/Silver-Task.Server.csproj -c Release -o "$new_publish_dir"
    ) >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1; then
        rm -rf "$new_publish_dir"
        ST_UP_STATE_ACTIVATION_STATUS="FAILED"
        st_up_state_write "FAILED" "$previous_version" "$target" "building target release" "$start_time" "$upgrade_id"
        st_error "UPGRADE FAILED"; st_error "ACTIVATION_FAILED"
        print_recovery_info "ACTIVATION_FAILED (build)" "The running application was never stopped and is unaffected."
        st_up_log "activate: FAILED, could not build target release $target"
        exit 19
    fi
    ST_UP_STATE_ACTIVATION_STATUS="BUILT"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Target release built")
    st_info "[OK] Target release built (old application still running, untouched)"

    # --- Enter maintenance mode — the OLD, still-running app immediately starts returning 503. ---
    st_step "Enabling maintenance mode"
    if ! st_up_maintenance_enable "$upgrade_id" "$target"; then
        rm -rf "$new_publish_dir"
        ST_UP_STATE_MAINTENANCE_STATUS="FAILED"
        st_up_state_write "FAILED" "$previous_version" "$target" "enabling maintenance mode" "$start_time" "$upgrade_id"
        st_error "UPGRADE FAILED"; st_error "MAINTENANCE_MODE_FAILED"
        print_recovery_info "MAINTENANCE_MODE_FAILED" "The running application was never stopped and is unaffected."
        st_up_log "activate: FAILED, could not enable maintenance mode"
        exit 18
    fi
    ST_UP_STATE_MAINTENANCE_STATUS="ACTIVE"
    st_up_state_write "MAINTENANCE_ENABLED" "$previous_version" "$target" "maintenance mode active" "$start_time" "$upgrade_id"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Maintenance mode enabled")
    st_info "[OK] Maintenance mode enabled"

    # --- Every failure from here on keeps maintenance mode ACTIVE — brief: "keep maintenance mode
    # active unless safe recovery restores service," and Phase 54 does not implement that recovery
    # yet. Surfaced loudly in every recovery-info block below, never silently left ambiguous. ---

    st_step "Stopping service"
    if ! systemctl stop "$SILVERTASK_SERVICE_NAME" >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1; then
        ST_UP_STATE_SERVICE_STATUS="FAILED"
        st_up_state_write "FAILED" "$previous_version" "$target" "stopping service" "$start_time" "$upgrade_id"
        st_error "UPGRADE FAILED"; st_error "SERVICE_START_FAILED"
        print_recovery_info "SERVICE_STOP_FAILED" "Maintenance mode is still ACTIVE."
        st_up_log "activate: FAILED, could not stop $SILVERTASK_SERVICE_NAME"
        exit 20
    fi

    st_step "Activating release (swapping in $target)"
    local previous_publish_dir="${SILVERTASK_PUBLISH_DIR}.previous"
    rm -rf "$previous_publish_dir"
    if ! (
        [ -d "$SILVERTASK_PUBLISH_DIR" ] && mv "$SILVERTASK_PUBLISH_DIR" "$previous_publish_dir"
        mv "$new_publish_dir" "$SILVERTASK_PUBLISH_DIR"
    ) >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1; then
        ST_UP_STATE_ACTIVATION_STATUS="FAILED"
        st_up_state_write "FAILED" "$previous_version" "$target" "swapping release directories" "$start_time" "$upgrade_id"
        st_error "UPGRADE FAILED"; st_error "ACTIVATION_FAILED"
        print_recovery_info "ACTIVATION_FAILED (directory swap)" "Maintenance mode is still ACTIVE, service is STOPPED — manual recovery required. Check $SILVERTASK_PUBLISH_DIR / $previous_publish_dir / $new_publish_dir by hand before doing anything else."
        st_up_log "activate: FAILED, directory swap failed — manual recovery required"
        exit 19
    fi
    chown -R "$SILVERTASK_SERVICE_USER:$SILVERTASK_SERVICE_USER" "$SILVERTASK_PUBLISH_DIR"
    ST_UP_STATE_ACTIVATION_STATUS="OK"
    st_up_state_write "ACTIVATING" "$previous_version" "$target" "release activated, running migrations" "$start_time" "$upgrade_id"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Release activated (previous kept at $previous_publish_dir)")
    st_info "[OK] Release activated — previous version preserved at $previous_publish_dir"

    st_step "Running database migrations"
    st_up_state_write "MIGRATING" "$previous_version" "$target" "running database migrations" "$start_time" "$upgrade_id"
    if ! (
        cd "$SILVERTASK_SOURCE_DIR"
        st_load_env_file "$SILVERTASK_ENV_FILE"
        dotnet ef database update --project Silver-Task.Server --startup-project Silver-Task.Server
    ) >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1; then
        ST_UP_STATE_MIGRATION_EXECUTION_STATUS="FAILED"
        st_up_state_write "FAILED" "$previous_version" "$target" "running database migrations" "$start_time" "$upgrade_id"
        st_error "UPGRADE FAILED"; st_error "MIGRATION_FAILED"
        print_recovery_info "MIGRATION_FAILED" "Maintenance mode is still ACTIVE, service is STOPPED. Do not restore the database automatically — see docs/restore.md and the exact failed migration in $SILVERTASK_UPGRADE_LOG_FILE."
        st_up_log "activate: FAILED, migration failed — see $SILVERTASK_UPGRADE_LOG_FILE for the exact migration"
        exit 21
    fi
    ST_UP_STATE_MIGRATION_EXECUTION_STATUS="OK"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Migrations completed")
    st_info "[OK] Migrations completed"

    st_step "Starting service"
    st_up_state_write "STARTING_SERVICES" "$previous_version" "$target" "starting service" "$start_time" "$upgrade_id"
    if ! systemctl start "$SILVERTASK_SERVICE_NAME" >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1; then
        ST_UP_STATE_SERVICE_STATUS="FAILED"
        st_up_state_write "FAILED" "$previous_version" "$target" "starting service" "$start_time" "$upgrade_id"
        st_error "UPGRADE FAILED"; st_error "SERVICE_START_FAILED"
        print_recovery_info "SERVICE_START_FAILED" "Maintenance mode is still ACTIVE. Check: systemctl status $SILVERTASK_SERVICE_NAME, journalctl -u $SILVERTASK_SERVICE_NAME -n 100."
        st_up_log "activate: FAILED, could not start $SILVERTASK_SERVICE_NAME"
        exit 20
    fi
    ST_UP_STATE_SERVICE_STATUS="OK"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Service started")
    st_info "[OK] Service started"

    st_step "Running health checks"
    st_up_state_write "VALIDATING" "$previous_version" "$target" "running health checks" "$start_time" "$upgrade_id"
    if ! st_health_check "http://127.0.0.1:5000" 15 3; then
        ST_UP_STATE_HEALTH_CHECK_STATUS="FAILED"
        st_up_state_write "FAILED" "$previous_version" "$target" "health check" "$start_time" "$upgrade_id"
        st_error "UPGRADE FAILED"; st_error "HEALTH_CHECK_FAILED"
        print_recovery_info "HEALTH_CHECK_FAILED" "Maintenance mode is still ACTIVE. Check: systemctl status $SILVERTASK_SERVICE_NAME, journalctl -u $SILVERTASK_SERVICE_NAME -n 100."
        st_up_log "activate: FAILED, health check timeout"
        exit 22
    fi
    ST_UP_STATE_HEALTH_CHECK_STATUS="OK"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Health checks passed")
    st_info "[OK] Health checks passed"

    st_step "Validating version consistency"
    local running_version
    running_version="$(st_up_running_version "http://127.0.0.1:5000" || true)"
    if [ "$running_version" != "$target" ]; then
        ST_UP_STATE_VERSION_VALIDATION_STATUS="FAILED"
        st_up_state_write "FAILED" "$previous_version" "$target" "version validation" "$start_time" "$upgrade_id"
        st_error "UPGRADE FAILED"; st_error "VERSION_VALIDATION_FAILED"
        st_error "Target Version: $target | Running Backend Version: ${running_version:-unreachable}"
        print_recovery_info "VERSION_VALIDATION_FAILED" "Maintenance mode is still ACTIVE."
        st_up_log "activate: FAILED, version mismatch (target=$target running=${running_version:-unreachable})"
        exit 23
    fi
    # Best-effort frontend check — a literal grep for the version string the Phase-51 footer bakes
    # into the built JS bundle (VersionFooter.tsx). Not fatal if the asset can't be located/parsed
    # (logged only, backend version already proved consistency above); a CONFIRMED mismatch (asset
    # found, wrong version inside it) is real signal and is treated as a hard failure.
    local index_html bundle_path frontend_check="undetectable"
    index_html="$(curl -fsS --max-time 5 "http://127.0.0.1:5000/" 2>/dev/null || true)"
    bundle_path="$(printf '%s' "$index_html" | grep -oE '/assets/index-[A-Za-z0-9_-]+\.js' | head -1)"
    if [ -n "$bundle_path" ]; then
        if curl -fsS --max-time 5 "http://127.0.0.1:5000$bundle_path" 2>/dev/null | grep -q "Silver Task v$target"; then
            frontend_check="confirmed"
        else
            frontend_check="mismatch"
        fi
    fi
    case "$frontend_check" in
        mismatch)
            ST_UP_STATE_VERSION_VALIDATION_STATUS="FAILED"
            st_up_state_write "FAILED" "$previous_version" "$target" "version validation" "$start_time" "$upgrade_id"
            st_error "UPGRADE FAILED"; st_error "VERSION_VALIDATION_FAILED"
            st_error "Served frontend bundle does not contain the expected version string \"Silver Task v$target\"."
            print_recovery_info "VERSION_VALIDATION_FAILED (frontend)" "Maintenance mode is still ACTIVE."
            st_up_log "activate: FAILED, frontend version mismatch"
            exit 23
            ;;
        confirmed) st_info "[OK] Frontend bundle confirms version $target" ;;
        *) st_warn "Could not confirm frontend version from the served bundle (non-fatal — backend version already confirmed)." ;;
    esac
    ST_UP_STATE_VERSION_VALIDATION_STATUS="OK"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Version validated")
    st_info "[OK] Backend version confirmed: $target"

    st_step "Running smoke tests"
    # Deliberately no authenticated calls — no service account exists to make one safely, and the
    # brief itself says not to modify production data merely to test the upgrade. This checks the
    # SPA shell is actually served; API/database reachability were already proven by the health
    # check above.
    local smoke_status
    smoke_status="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:5000/" 2>/dev/null || echo 000)"
    if [ "$smoke_status" != "200" ]; then
        ST_UP_STATE_SMOKE_TEST_STATUS="FAILED"
        st_up_state_write "FAILED" "$previous_version" "$target" "smoke tests" "$start_time" "$upgrade_id"
        st_error "UPGRADE FAILED"; st_error "SMOKE_TEST_FAILED"
        st_error "GET / returned HTTP $smoke_status, expected 200."
        print_recovery_info "SMOKE_TEST_FAILED" "Maintenance mode is still ACTIVE."
        st_up_log "activate: FAILED, smoke test failed (GET / -> $smoke_status)"
        exit 24
    fi
    ST_UP_STATE_SMOKE_TEST_STATUS="OK"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Smoke tests passed")
    st_info "[OK] Smoke tests passed (SPA shell reachable, API health reachable, database reachable)"

    # --- Only now: commit the installed version. Everything above has proven the new release is
    # actually healthy; nothing before this point ever touched installed-version.json. ---
    st_step "Committing installed version"
    local new_commit
    new_commit="$(git -C "$SILVERTASK_SOURCE_DIR" rev-parse --short HEAD)"
    cat > "$SILVERTASK_INSTALL_DIR/installed-version.json" <<EOF
{
  "version": "$target",
  "gitCommit": "$new_commit",
  "installedAtUtc": "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
}
EOF
    chown "$SILVERTASK_SERVICE_USER:$SILVERTASK_SERVICE_USER" "$SILVERTASK_INSTALL_DIR/installed-version.json"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Installed version committed")
    st_info "[OK] Installed version committed: $target"

    st_step "Disabling maintenance mode"
    st_up_maintenance_disable
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Maintenance mode disabled")
    st_info "[OK] Maintenance mode disabled — normal traffic restored"

    st_step "Final availability check"
    local final_status
    final_status="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:5000/api/health" 2>/dev/null || echo 000)"
    if [ "$final_status" != "200" ]; then
        st_error "UPGRADE COMPLETION ERROR — APPLICATION NOT AVAILABLE"
        st_error "GET /api/health returned HTTP $final_status after maintenance mode was disabled."
        st_up_log "activate: WARNING, final availability check failed after commit ($final_status) — installed version was already committed, investigate immediately"
        # Deliberately not a nonzero exit that implies the whole upgrade failed — the version IS
        # committed and migrations DID run; this is specifically "came back unhealthy after
        # traffic was restored," the brief's own distinct case, surfaced loudly rather than
        # silently claiming success.
    else
        st_info "[OK] Final availability check passed"
    fi

    ST_UP_STATE_COMPLETED_AT="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
    st_up_state_write "COMPLETED" "$previous_version" "$target" "upgrade complete" "$start_time" "$upgrade_id"
    timeline+=("$ST_UP_STATE_COMPLETED_AT Upgrade completed")
    st_up_log "activate: COMPLETED, upgrade $upgrade_id ($previous_version -> $target)"

    echo ""
    echo "Timeline:"
    local entry
    for entry in "${timeline[@]}"; do
        echo "  $entry"
    done
    echo ""
    st_info "=================================================================="
    st_info " UPGRADE COMPLETE"
    st_info " $previous_version -> $target"
    st_info " Upgrade ID: $upgrade_id"
    st_info " Previous release preserved at: $previous_publish_dir"
    st_info " Pre-upgrade backup: $backup_dir"
    st_info "=================================================================="
    exit 0
}

cmd_rollback() {
    st_up_log "rollback: starting (yes=$ASSUME_YES, restore-config=$RESTORE_CONFIG, dry-run=$DRY_RUN)"

    if ! st_rb_eligibility_check; then
        if [ "$ST_RB_TARGET_UNAVAILABLE" = true ]; then
            st_error "ROLLBACK TARGET UNAVAILABLE"
            st_error "$ST_RB_BLOCKED_REASON"
            st_up_log "rollback: BLOCKED (target unavailable), $ST_RB_BLOCKED_REASON"
            exit 27
        fi
        st_error "ROLLBACK BLOCKED"
        st_error "$ST_RB_BLOCKED_REASON"
        st_up_log "rollback: BLOCKED, $ST_RB_BLOCKED_REASON"
        exit 26
    fi
    local rollback_target="$ST_RB_TARGET_VERSION"
    local failed_version="$ST_RB_FAILED_VERSION"
    local related_upgrade_id="$ST_RB_RELATED_UPGRADE_ID"
    local backup_dir="$ST_RB_BACKUP_DIR"

    st_rb_database_decision
    local db_decision="$ST_RB_DB_DECISION"
    if [ "$db_decision" = "MANUAL_RECOVERY_REQUIRED" ]; then
        st_error "ROLLBACK BLOCKED"
        st_error "Database compatibility could not be determined (migrationRequired is unknown for upgrade $related_upgrade_id) — manual recovery required. See docs/rollback.md \"Manual recovery.\""
        st_up_log "rollback: BLOCKED, MANUAL_RECOVERY_REQUIRED"
        exit 26
    fi

    local reason="${ROLLBACK_REASON:-Administrator requested rollback}"

    echo "Silver Task Rollback"
    echo ""
    echo "Current Version: $failed_version"
    echo "Rollback Target: $rollback_target"
    echo "Related Upgrade ID: $related_upgrade_id"
    echo ""
    echo "Application Release Available: YES"
    echo "Database Backup Available: YES"
    echo "Configuration Backup Available: YES"
    echo ""
    echo "Reason for rollback: $reason"
    echo ""
    echo "Rollback Plan"
    echo ""
    echo "   1. Enable maintenance mode"
    echo "   2. Preserve current failed release"
    echo "   3. Switch application to $rollback_target"
    echo "   4. Determine database recovery requirement (decision: $db_decision)"
    if [ "$db_decision" = "DATABASE_RESTORE_REQUIRED" ]; then
        echo "   5. Create an emergency backup of the current database"
        echo "   6. Restore the pre-upgrade database backup"
    else
        echo "   5. (skipped — no schema migration occurred; application-only rollback)"
        echo "   6. (skipped)"
    fi
    if [ "$RESTORE_CONFIG" = true ]; then
        echo "   7. Restore configuration from the pre-upgrade backup"
    else
        echo "   7. (skipped — configuration restore not requested; pass --restore-config to include it)"
    fi
    echo "   8. Restart required services"
    echo "   9. Validate application health"
    echo "  10. Validate rollback version"
    echo "  11. Restore normal application access"
    echo ""

    if [ "$DRY_RUN" = true ]; then
        echo "DRY RUN — no changes made."
        echo "Database Restore Decision: $db_decision"
        echo "Configuration Restore: $([ "$RESTORE_CONFIG" = true ] && echo REQUESTED || echo "NOT REQUESTED (default)")"
        echo "Release Switch Plan: preserve $SILVERTASK_PUBLISH_DIR at \${SILVERTASK_PUBLISH_DIR}.failed,"
        echo "  reactivate the preserved \${SILVERTASK_PUBLISH_DIR}.previous."
        echo "Service Restart Plan: systemctl stop then start $SILVERTASK_SERVICE_NAME only."
        echo "Health Validation Plan: poll /api/health/ready, then confirm backend + best-effort"
        echo "  frontend version equal $rollback_target, then GET / smoke test."
        echo ""
        echo "None of the above runs until a separate, explicitly confirmed 'sudo"
        echo "./scripts/update-debian.sh --rollback' without --dry-run."
        st_up_log "rollback: dry-run complete ($failed_version -> $rollback_target, decision=$db_decision)"
        exit 0
    fi

    if [ "$ASSUME_YES" != true ]; then
        local reply=""
        read -r -p "Continue with rollback? [y/N]: " reply || true
        case "$reply" in
            y|Y) ;;
            *)
                st_info "Aborted by administrator."
                st_up_log "rollback: aborted by administrator"
                exit 0
                ;;
        esac
    fi

    if [ "$db_decision" = "DATABASE_RESTORE_REQUIRED" ] && [ "$ASSUME_YES" != true ]; then
        echo ""
        echo "WARNING: this will restore the database to its pre-upgrade state, discarding any data"
        echo "created or modified since that backup was taken ($backup_dir)."
        if ! st_confirm_destructive "About to restore the database to version $rollback_target's pre-upgrade state." "$rollback_target"; then
            st_info "Aborted by administrator (database restore not confirmed)."
            st_up_log "rollback: aborted, database restore confirmation declined"
            exit 0
        fi
    fi

    if [ "$FORCE_NO_EMERGENCY_BACKUP" = true ] && [ "$ASSUME_YES" != true ]; then
        echo ""
        echo "WARNING: --force-no-emergency-backup skips backing up the current database before"
        echo "restoring — if the restore goes wrong, the current (pre-rollback) data cannot be recovered."
        if ! st_confirm_destructive "Skip the emergency backup?" "skip backup"; then
            st_info "Aborted by administrator (emergency-backup skip not confirmed)."
            st_up_log "rollback: aborted, emergency-backup skip not confirmed"
            exit 0
        fi
    fi

    st_step "Checking maintenance-window policy"
    if ! st_up_maintenance_window_check "$SILVERTASK_ENV_FILE"; then
        if [ "$OVERRIDE_MAINTENANCE_WINDOW" = true ]; then
            echo ""
            echo "WARNING: the current time is outside the configured maintenance window"
            echo "($ST_UP_MAINTENANCE_WINDOW) — --override-maintenance-window was passed."
            if ! st_confirm_destructive "About to roll back outside the configured maintenance window." "$rollback_target"; then
                st_info "Aborted by administrator (maintenance-window override not confirmed)."
                st_up_log "rollback: aborted, maintenance-window override not confirmed"
                exit 0
            fi
            st_warn "Proceeding outside the maintenance window ($ST_UP_MAINTENANCE_WINDOW) — override confirmed."
        else
            st_error "ROLLBACK BLOCKED BY MAINTENANCE WINDOW POLICY"
            st_error "Upgrade__MaintenanceWindow=$ST_UP_MAINTENANCE_WINDOW is configured and the current time is outside it. Pass --override-maintenance-window to proceed anyway (requires confirmation)."
            st_up_log "rollback: BLOCKED, outside maintenance window $ST_UP_MAINTENANCE_WINDOW"
            exit 35
        fi
    elif [ "$ST_UP_MAINTENANCE_WINDOW_CONFIGURED" = true ]; then
        st_info "[OK] Inside configured maintenance window ($ST_UP_MAINTENANCE_WINDOW)"
    fi

    st_step "Acquiring upgrade lock"
    if ! st_up_lock_acquire "$rollback_target" "rollback"; then
        st_up_lock_probe || true
        st_error "UPGRADE ALREADY IN PROGRESS (operation: ${ST_UP_LOCK_OPERATION_TYPE:-unknown}, target ${ST_UP_LOCK_TARGET:-unknown}, started ${ST_UP_LOCK_STARTED:-unknown}, pid ${ST_UP_LOCK_PID:-unknown})."
        st_up_log "rollback: aborted, lock held by pid ${ST_UP_LOCK_PID:-unknown}"
        exit 6
    fi
    local rollback_id start_time
    rollback_id="$(st_rb_generate_rollback_id)"
    start_time="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
    local -a timeline=("$start_time Rollback requested")

    # Same crash-safety guarantee as cmd_prepare/cmd_activate: flock auto-releases if this process
    # dies; the trap makes a clean exit (success or a handled failure below) release the lock and
    # record this attempt in the durable release history — one trap covers every failure branch
    # below instead of a call at each one, mirroring cmd_activate's st_activate_finalize.
    st_rollback_finalize() {
        local rc=$?
        st_up_lock_release
        if [ "$rc" -eq 0 ]; then
            st_up_history_append "rollback" "$rollback_id" "$failed_version" "$rollback_target" "COMPLETED" "$reason"
        else
            st_up_history_append "rollback" "$rollback_id" "$failed_version" "$rollback_target" "FAILED" "$reason (exit code $rc)"
        fi
    }
    trap 'st_rollback_finalize' EXIT
    st_info "Rollback ID: $rollback_id"

    ST_RB_STATE_DB_DECISION="$db_decision"
    ST_RB_STATE_DB_RESTORE_PERFORMED="false"
    ST_RB_STATE_CONFIG_RESTORE_PERFORMED="false"
    ST_RB_STATE_EMERGENCY_BACKUP_DIR=""
    ST_RB_STATE_HEALTH_CHECK_STATUS="PENDING"
    ST_RB_STATE_VERSION_VALIDATION_STATUS="PENDING"
    ST_RB_STATE_SMOKE_TEST_STATUS="PENDING"
    st_rb_state_write "ROLLBACK_VALIDATING" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "validating rollback eligibility" "$start_time" "$rollback_id"
    st_up_log "rollback: lock acquired, rollback $rollback_id ($failed_version -> $rollback_target), reason: $reason"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Validation passed")

    # Printed on any failure path below — the same discipline cmd_activate's print_recovery_info
    # already established: never claim the previous version is active unless actually verified.
    print_rollback_recovery_info() {
        local failed_step="$1" extra="${2:-}"
        echo ""
        echo "Rollback failed during: $failed_step"
        echo ""
        echo "Failed (current) Release: $failed_version"
        echo "Rollback Target: $rollback_target"
        echo "Related Upgrade ID: $related_upgrade_id"
        echo "Rollback ID: $rollback_id"
        echo ""
        echo "Pre-upgrade Backup: $backup_dir"
        echo "Emergency Backup: ${ST_RB_STATE_EMERGENCY_BACKUP_DIR:-not created}"
        echo ""
        [ -n "$extra" ] && echo "$extra"
        echo "See $SILVERTASK_UPGRADE_LOG_FILE for details. Administrator review required — no"
        echo "further automatic recovery will be attempted."
    }

    st_step "Creating emergency backup of the current database"
    if [ "$FORCE_NO_EMERGENCY_BACKUP" = true ]; then
        st_warn "Skipping emergency backup (--force-no-emergency-backup, confirmed above)."
        st_up_log "rollback: emergency backup skipped (--force-no-emergency-backup)"
    else
        st_rb_state_write "ROLLBACK_PREPARING" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "creating emergency backup" "$start_time" "$rollback_id"
        if ! st_up_run_backup "$SCRIPT_DIR" "emergency-pre-rollback" "$rollback_id" "$failed_version" "$rollback_target"; then
            st_rb_state_write "ROLLBACK_FAILED" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "creating emergency backup" "$start_time" "$rollback_id"
            st_error "ROLLBACK FAILED"; st_error "EMERGENCY_BACKUP_FAILED"
            print_rollback_recovery_info "EMERGENCY_BACKUP_FAILED" "The current (pre-rollback) database was NOT touched. The failed application release is still active."
            st_up_log "rollback: FAILED, emergency backup failed (result: ${ST_UP_BACKUP_RESULT:-unknown})"
            exit 28
        fi
        ST_RB_STATE_EMERGENCY_BACKUP_DIR="$ST_UP_BACKUP_DIR"
        timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Emergency database backup created")
        st_info "[OK] Emergency backup created and verified: $ST_UP_BACKUP_DIR"
    fi

    st_step "Enabling maintenance mode"
    st_rb_state_write "ROLLBACK_MAINTENANCE" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "enabling maintenance mode" "$start_time" "$rollback_id"
    if ! st_up_maintenance_enable "$rollback_id" "$rollback_target"; then
        st_rb_state_write "ROLLBACK_FAILED" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "enabling maintenance mode" "$start_time" "$rollback_id"
        st_error "ROLLBACK FAILED"; st_error "MAINTENANCE_MODE_FAILED"
        print_rollback_recovery_info "MAINTENANCE_MODE_FAILED" "The failed application release is still active and was never stopped."
        st_up_log "rollback: FAILED, could not enable maintenance mode"
        exit 18
    fi
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Maintenance mode enabled")
    st_info "[OK] Maintenance mode enabled"

    # --- Every failure from here on keeps maintenance mode ACTIVE, same discipline as
    # cmd_activate — Phase 55 does not implement automatic recovery from a failed rollback. ---

    st_step "Stopping service"
    if ! systemctl stop "$SILVERTASK_SERVICE_NAME" >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1; then
        st_rb_state_write "ROLLBACK_FAILED" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "stopping service" "$start_time" "$rollback_id"
        st_error "ROLLBACK FAILED"; st_error "SERVICE_START_FAILED"
        print_rollback_recovery_info "SERVICE_STOP_FAILED" "Maintenance mode is still ACTIVE."
        st_up_log "rollback: FAILED, could not stop $SILVERTASK_SERVICE_NAME"
        exit 31
    fi

    st_step "Switching application to $rollback_target"
    st_rb_state_write "ROLLBACK_APPLICATION" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "switching release" "$start_time" "$rollback_id"
    if ! st_rb_switch_release "$rollback_target" "$related_upgrade_id" "$failed_version" "post-activation failure"; then
        st_rb_state_write "ROLLBACK_FAILED" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "switching release" "$start_time" "$rollback_id"
        st_error "ROLLBACK FAILED"; st_error "ACTIVATION_FAILED"
        print_rollback_recovery_info "ACTIVATION_FAILED (release switch)" "Maintenance mode is still ACTIVE, service is STOPPED — manual recovery required. Check $SILVERTASK_PUBLISH_DIR / ${SILVERTASK_PUBLISH_DIR}.previous / ${SILVERTASK_PUBLISH_DIR}.failed by hand before doing anything else."
        st_up_log "rollback: FAILED, release switch failed — manual recovery required"
        exit 19
    fi
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Previous release activated (failed release preserved at ${SILVERTASK_PUBLISH_DIR}.failed)")
    st_info "[OK] Application switched to $rollback_target — failed release preserved at ${SILVERTASK_PUBLISH_DIR}.failed"

    if [ "$db_decision" = "DATABASE_RESTORE_REQUIRED" ]; then
        st_step "Restoring pre-upgrade database backup"
        st_rb_state_write "ROLLBACK_DATABASE" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "restoring database" "$start_time" "$rollback_id"
        if ! ( st_rb_restore_database "$backup_dir" "$SILVERTASK_ENV_FILE" ) >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1; then
            st_rb_state_write "ROLLBACK_FAILED" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "restoring database" "$start_time" "$rollback_id"
            st_error "ROLLBACK FAILED"; st_error "DATABASE_RESTORE_FAILED"
            print_rollback_recovery_info "DATABASE_RESTORE_FAILED" "Maintenance mode is still ACTIVE, service is STOPPED. The emergency backup (${ST_RB_STATE_EMERGENCY_BACKUP_DIR:-none taken}) reflects the database state immediately before this restore attempt."
            st_up_log "rollback: FAILED, database restore failed"
            exit 29
        fi
        timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Database restored")
        st_info "[OK] Database restored from $backup_dir"

        st_step "Validating restored database"
        if ! st_rb_validate_database "$SILVERTASK_ENV_FILE"; then
            st_rb_state_write "ROLLBACK_FAILED" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "validating restored database" "$start_time" "$rollback_id"
            st_error "ROLLBACK FAILED"; st_error "DATABASE_RESTORE_FAILED"
            print_rollback_recovery_info "DATABASE_RESTORE_FAILED (validation)" "Maintenance mode is still ACTIVE, service is STOPPED. The restore command completed but the resulting database state could not be validated as compatible with $rollback_target."
            st_up_log "rollback: FAILED, restored database failed validation"
            exit 29
        fi
        ST_RB_STATE_DB_RESTORE_PERFORMED="true"
        timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Database validated")
        st_info "[OK] Restored database validated"
    else
        st_info "[OK] No database restore required (application-only rollback)"
    fi

    if [ "$RESTORE_CONFIG" = true ]; then
        st_step "Restoring configuration"
        st_rb_state_write "ROLLBACK_CONFIGURATION" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "restoring configuration" "$start_time" "$rollback_id"
        if ! st_rb_restore_configuration "$backup_dir" "$SILVERTASK_ENV_FILE"; then
            st_rb_state_write "ROLLBACK_FAILED" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "restoring configuration" "$start_time" "$rollback_id"
            st_error "ROLLBACK FAILED"; st_error "CONFIGURATION_RESTORE_FAILED"
            print_rollback_recovery_info "CONFIGURATION_RESTORE_FAILED" "Maintenance mode is still ACTIVE, service is STOPPED."
            st_up_log "rollback: FAILED, configuration restore failed"
            exit 30
        fi
        ST_RB_STATE_CONFIG_RESTORE_PERFORMED="true"
        timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Configuration restored")
        st_info "[OK] Configuration restored (emergency copy: $ST_RB_CONFIG_EMERGENCY_COPY)"
    fi

    st_step "Starting service"
    st_rb_state_write "ROLLBACK_SERVICES" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "starting service" "$start_time" "$rollback_id"
    if ! systemctl start "$SILVERTASK_SERVICE_NAME" >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1; then
        st_rb_state_write "ROLLBACK_FAILED" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "starting service" "$start_time" "$rollback_id"
        st_error "ROLLBACK FAILED"; st_error "SERVICE_START_FAILED"
        print_rollback_recovery_info "SERVICE_START_FAILED" "Maintenance mode is still ACTIVE. Check: systemctl status $SILVERTASK_SERVICE_NAME, journalctl -u $SILVERTASK_SERVICE_NAME -n 100."
        st_up_log "rollback: FAILED, could not start $SILVERTASK_SERVICE_NAME"
        exit 31
    fi
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Service started")
    st_info "[OK] Service started"

    st_step "Running health checks"
    st_rb_state_write "ROLLBACK_HEALTH_VALIDATION" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "running health checks" "$start_time" "$rollback_id"
    if ! st_health_check "http://127.0.0.1:5000" 15 3; then
        ST_RB_STATE_HEALTH_CHECK_STATUS="FAILED"
        st_rb_state_write "ROLLBACK_FAILED" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "health check" "$start_time" "$rollback_id"
        st_error "ROLLBACK FAILED"; st_error "HEALTH_CHECK_FAILED"
        print_rollback_recovery_info "HEALTH_CHECK_FAILED" "Maintenance mode is still ACTIVE. Check: systemctl status $SILVERTASK_SERVICE_NAME, journalctl -u $SILVERTASK_SERVICE_NAME -n 100."
        st_up_log "rollback: FAILED, health check timeout"
        exit 32
    fi
    ST_RB_STATE_HEALTH_CHECK_STATUS="OK"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Health checks passed")
    st_info "[OK] Health checks passed"

    st_step "Validating rollback version"
    local running_version
    running_version="$(st_up_running_version "http://127.0.0.1:5000" || true)"
    if [ "$running_version" != "$rollback_target" ]; then
        ST_RB_STATE_VERSION_VALIDATION_STATUS="FAILED"
        st_rb_state_write "ROLLBACK_FAILED" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "version validation" "$start_time" "$rollback_id"
        st_error "ROLLBACK FAILED"; st_error "VERSION_VALIDATION_FAILED"
        st_error "Rollback Target: $rollback_target | Running Backend Version: ${running_version:-unreachable}"
        print_rollback_recovery_info "VERSION_VALIDATION_FAILED" "Maintenance mode is still ACTIVE."
        st_up_log "rollback: FAILED, version mismatch (target=$rollback_target running=${running_version:-unreachable})"
        exit 33
    fi
    ST_RB_STATE_VERSION_VALIDATION_STATUS="OK"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Version validated")
    st_info "[OK] Backend version confirmed: $rollback_target"

    st_step "Running smoke tests"
    # Same scope/justification as cmd_activate: SPA shell reachability only, no authenticated
    # calls — no service account exists to make one safely, and production data is never touched
    # merely to test a rollback.
    local smoke_status
    smoke_status="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:5000/" 2>/dev/null || echo 000)"
    if [ "$smoke_status" != "200" ]; then
        ST_RB_STATE_SMOKE_TEST_STATUS="FAILED"
        st_rb_state_write "ROLLBACK_FAILED" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "smoke tests" "$start_time" "$rollback_id"
        st_error "ROLLBACK FAILED"; st_error "SMOKE_TEST_FAILED"
        st_error "GET / returned HTTP $smoke_status, expected 200."
        print_rollback_recovery_info "SMOKE_TEST_FAILED" "Maintenance mode is still ACTIVE."
        st_up_log "rollback: FAILED, smoke test failed (GET / -> $smoke_status)"
        exit 24
    fi
    ST_RB_STATE_SMOKE_TEST_STATUS="OK"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Smoke tests passed")
    st_info "[OK] Smoke tests passed"

    # --- Only now: commit the rollback target as the installed version. ---
    st_step "Committing rollback version"
    local rollback_commit
    rollback_commit="$(git -C "$SILVERTASK_SOURCE_DIR" rev-parse --short HEAD)"
    cat > "$SILVERTASK_INSTALL_DIR/installed-version.json" <<EOF
{
  "version": "$rollback_target",
  "gitCommit": "$rollback_commit",
  "installedAtUtc": "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
}
EOF
    chown "$SILVERTASK_SERVICE_USER:$SILVERTASK_SERVICE_USER" "$SILVERTASK_INSTALL_DIR/installed-version.json"
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Installed version committed")
    st_info "[OK] Installed version committed: $rollback_target"

    st_step "Disabling maintenance mode"
    st_up_maintenance_disable
    timeline+=("$(date -u '+%Y-%m-%dT%H:%M:%SZ') Maintenance mode disabled")
    st_info "[OK] Maintenance mode disabled — normal traffic restored"

    st_step "Final availability check"
    local final_status
    final_status="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:5000/api/health" 2>/dev/null || echo 000)"
    if [ "$final_status" != "200" ]; then
        st_error "ROLLBACK COMPLETION ERROR — APPLICATION NOT AVAILABLE"
        st_error "GET /api/health returned HTTP $final_status after maintenance mode was disabled."
        st_up_log "rollback: WARNING, final availability check failed after commit ($final_status) — investigate immediately"
    else
        st_info "[OK] Final availability check passed"
    fi

    ST_RB_STATE_COMPLETED_AT="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
    st_rb_state_write "ROLLBACK_COMPLETED" "$related_upgrade_id" "$failed_version" "$rollback_target" "$reason" "rollback complete" "$start_time" "$rollback_id"
    timeline+=("$ST_RB_STATE_COMPLETED_AT Rollback completed")
    st_up_log "rollback: COMPLETED, rollback $rollback_id ($failed_version -> $rollback_target)"

    echo ""
    echo "Timeline:"
    local entry
    for entry in "${timeline[@]}"; do
        echo "  $entry"
    done
    echo ""
    st_info "=================================================================="
    st_info " ROLLBACK COMPLETE"
    st_info " $failed_version -> $rollback_target"
    st_info " Rollback ID: $rollback_id"
    st_info " Failed release preserved at: ${SILVERTASK_PUBLISH_DIR}.failed"
    st_info " Emergency backup: ${ST_RB_STATE_EMERGENCY_BACKUP_DIR:-not created}"
    st_info "=================================================================="
    exit 0
}

cmd_history() {
    echo "Silver Task Upgrade/Rollback History (most recent first, limit $HISTORY_LIMIT)"
    echo ""
    local lines
    if ! lines="$(st_up_history_read "$HISTORY_LIMIT")" || [ -z "$lines" ]; then
        echo "No history yet — no activation or rollback has completed on this installation."
        st_up_log "history: no history yet"
        exit 0
    fi
    local ts type id from_ver to_ver status reason
    while IFS= read -r line; do
        [ -n "$line" ] || continue
        ts="$(printf '%s' "$line" | sed -n 's/.*"timestamp"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
        type="$(printf '%s' "$line" | sed -n 's/.*"type"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
        id="$(printf '%s' "$line" | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
        from_ver="$(printf '%s' "$line" | sed -n 's/.*"fromVersion"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
        to_ver="$(printf '%s' "$line" | sed -n 's/.*"toVersion"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
        status="$(printf '%s' "$line" | sed -n 's/.*"status"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
        reason="$(printf '%s' "$line" | sed -n 's/.*"reason"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
        echo "$ts  [$type] $from_ver -> $to_ver  $status  (id: $id)$([ -n "$reason" ] && echo "  reason: $reason")"
    done <<< "$lines"
    st_up_log "history: displayed $(printf '%s\n' "$lines" | grep -c '.') entries (limit $HISTORY_LIMIT)"
    exit 0
}

cmd_doctor() {
    echo "Silver Task Preflight Check (--doctor)"
    echo "Read-only — nothing below modifies the installation."
    echo ""
    local fail=0 warn=0

    st_step "Checking required tools on PATH"
    local tool
    for tool in git dotnet pg_dump pg_restore curl openssl; do
        if command -v "$tool" >/dev/null 2>&1; then
            echo "  PASS  $tool"
        else
            echo "  FAIL  $tool not found on PATH"
            fail=$((fail + 1))
        fi
    done

    st_step "Checking dotnet-ef (pinned local tool)"
    if command -v dotnet >/dev/null 2>&1 && (cd "$SILVERTASK_SOURCE_DIR" && dotnet tool restore) >/dev/null 2>&1; then
        echo "  PASS  dotnet-ef restorable from $SILVERTASK_SOURCE_DIR"
    else
        echo "  FAIL  dotnet-ef could not be restored (dotnet tool restore failed in $SILVERTASK_SOURCE_DIR)"
        fail=$((fail + 1))
    fi

    st_step "Checking environment configuration"
    if [ -f "$SILVERTASK_ENV_FILE" ]; then
        echo "  PASS  environment file found: $SILVERTASK_ENV_FILE"
        local key missing=""
        for key in ConnectionStrings__DefaultConnection Jwt__Secret; do
            grep -q "^${key}=" "$SILVERTASK_ENV_FILE" 2>/dev/null || missing="$missing $key"
        done
        if [ -z "$missing" ]; then
            echo "  PASS  required configuration keys present (values never printed)"
        else
            echo "  FAIL  missing configuration keys:$missing"
            fail=$((fail + 1))
        fi
    else
        echo "  FAIL  environment file not found: $SILVERTASK_ENV_FILE"
        fail=$((fail + 1))
    fi

    st_step "Checking version consistency"
    st_up_version_consistency
    case "$ST_UP_CONSISTENCY" in
        MATCH) echo "  PASS  installed/running version consistent (${ST_UP_INSTALLED:-unknown})" ;;
        UNKNOWN)
            echo "  WARN  could not determine installed and/or running version"
            warn=$((warn + 1))
            ;;
        *)
            echo "  FAIL  installed/running version mismatch (installed=${ST_UP_INSTALLED:-unknown} running=${ST_UP_RUNNING:-unknown})"
            fail=$((fail + 1))
            ;;
    esac

    st_step "Checking upgrade lock and maintenance mode"
    if st_up_lock_probe; then
        echo "  PASS  no upgrade/rollback lock currently held"
    else
        echo "  WARN  upgrade lock currently held (operation: ${ST_UP_LOCK_OPERATION_TYPE:-unknown}, started ${ST_UP_LOCK_STARTED:-unknown}, pid ${ST_UP_LOCK_PID:-unknown})"
        warn=$((warn + 1))
    fi
    st_up_maintenance_probe
    if [ "$ST_UP_MAINTENANCE_ACTIVE" = true ]; then
        echo "  WARN  maintenance mode is currently active (upgrade ${ST_UP_MAINTENANCE_UPGRADE_ID:-unknown}, started ${ST_UP_MAINTENANCE_STARTED:-unknown})"
        warn=$((warn + 1))
    else
        echo "  PASS  maintenance mode is not active"
    fi

    st_step "Checking maintenance-window policy"
    if st_up_maintenance_window_check "$SILVERTASK_ENV_FILE"; then
        if [ "$ST_UP_MAINTENANCE_WINDOW_CONFIGURED" = true ]; then
            echo "  PASS  Upgrade__MaintenanceWindow=$ST_UP_MAINTENANCE_WINDOW configured, currently inside the window"
        else
            echo "  PASS  no maintenance-window policy configured (default — activate/rollback allowed any time)"
        fi
    else
        echo "  WARN  Upgrade__MaintenanceWindow=$ST_UP_MAINTENANCE_WINDOW configured, currently OUTSIDE the window (activate/rollback would be blocked without --override-maintenance-window)"
        warn=$((warn + 1))
    fi

    st_step "Checking disk space"
    if st_up_disk_space_check; then
        echo "  PASS  disk space OK (${ST_UP_DISK_AVAILABLE_MB}MB available, ~${ST_UP_DISK_REQUIRED_MB}MB estimated needed for an upgrade)"
    else
        echo "  WARN  disk space may be insufficient for an upgrade (${ST_UP_DISK_AVAILABLE_MB}MB available, ~${ST_UP_DISK_REQUIRED_MB}MB estimated needed)"
        warn=$((warn + 1))
    fi

    echo ""
    echo "Summary: $fail FAIL, $warn WARN"
    st_up_log "doctor: $fail FAIL, $warn WARN"
    if [ "$fail" -gt 0 ]; then
        exit 36
    fi
    exit 0
}

case "$MODE" in
    check) cmd_check ;;
    status) cmd_status ;;
    prepare) cmd_prepare ;;
    activate) cmd_activate ;;
    rollback) cmd_rollback ;;
    history) cmd_history ;;
    doctor) cmd_doctor ;;
esac

# --- Legacy full update (Phase 51 and earlier — unchanged) ---

st_step "Checking installation"
if ! st_is_installed; then
    st_fail "Silver Task does not appear to be installed at $SILVERTASK_INSTALL_DIR." \
        "Run scripts/install-debian.sh first."
fi
st_info "Found existing installation at $SILVERTASK_INSTALL_DIR."

if [ "$SKIP_BACKUP" = true ]; then
    st_warn "Skipping pre-update backup (--skip-backup was passed). This is NOT recommended — see README 'Update safety'."
else
    st_step "Backing up before update"
    "$SCRIPT_DIR/backup-debian.sh" || st_fail "Pre-update backup failed — update aborted." \
        "Fix the backup problem first; updating without a verified backup is not safe. Use --skip-backup only if you understand the risk."
    st_info "Backup verified — proceeding with update."
fi

st_step "Fetching latest source"
if [ ! -d "$SILVERTASK_SOURCE_DIR/.git" ]; then
    st_fail "$SILVERTASK_SOURCE_DIR is not a git checkout — cannot update automatically." \
        "Re-run scripts/install-debian.sh from a git-cloned copy of the repository to restore update capability."
fi
st_trust_git_dir "$SILVERTASK_SOURCE_DIR"
(
    cd "$SILVERTASK_SOURCE_DIR"
    git fetch --all --tags >> "$SILVERTASK_LOG_FILE" 2>&1
    if [ -n "$REF" ]; then
        git checkout "$REF" >> "$SILVERTASK_LOG_FILE" 2>&1
    else
        default_branch="$(git remote show origin 2>/dev/null | awk '/HEAD branch/ {print $NF}')"
        git checkout "${default_branch:-main}" >> "$SILVERTASK_LOG_FILE" 2>&1
        git pull >> "$SILVERTASK_LOG_FILE" 2>&1
    fi
) || st_fail "git fetch/checkout failed." "Check $SILVERTASK_LOG_FILE and that $SILVERTASK_SOURCE_DIR has no uncommitted local changes."
NEW_COMMIT="$(cd "$SILVERTASK_SOURCE_DIR" && git rev-parse --short HEAD)"
st_info "Source updated to $NEW_COMMIT."

st_step "Checking version/git-tag compatibility"
NEW_VERSION="$("$SCRIPT_DIR/check-version.sh" "$SILVERTASK_SOURCE_DIR" | tee -a "$SILVERTASK_LOG_FILE" | awk -F': ' '/^Version:/ {print $2}')" \
    || st_fail "Version check failed — the checked-out ref's VERSION file and git tag disagree." \
        "See $SILVERTASK_LOG_FILE. This usually means a release tag was cut without updating VERSION — fix the tag or VERSION before updating."
st_info "Updating to version $NEW_VERSION."

st_step "Rebuilding (dotnet publish -c Release)"
PREVIOUS_PUBLISH_DIR="${SILVERTASK_PUBLISH_DIR}.previous"
rm -rf "$PREVIOUS_PUBLISH_DIR"
[ -d "$SILVERTASK_PUBLISH_DIR" ] && mv "$SILVERTASK_PUBLISH_DIR" "$PREVIOUS_PUBLISH_DIR"
(
    cd "$SILVERTASK_SOURCE_DIR"
    dotnet tool restore >> "$SILVERTASK_LOG_FILE" 2>&1
    dotnet publish Silver-Task.Server/Silver-Task.Server.csproj -c Release -o "$SILVERTASK_PUBLISH_DIR" >> "$SILVERTASK_LOG_FILE" 2>&1
) || {
    st_warn "Build failed — restoring previous build so the running service is unaffected."
    rm -rf "$SILVERTASK_PUBLISH_DIR"
    [ -d "$PREVIOUS_PUBLISH_DIR" ] && mv "$PREVIOUS_PUBLISH_DIR" "$SILVERTASK_PUBLISH_DIR"
    st_fail "Update aborted: build failed, previous version left running." "Check $SILVERTASK_LOG_FILE."
}
chown -R "$SILVERTASK_SERVICE_USER:$SILVERTASK_SERVICE_USER" "$SILVERTASK_PUBLISH_DIR"
st_info "Build succeeded."

# Written to SILVERTASK_INSTALL_DIR (stable across updates), not SILVERTASK_PUBLISH_DIR (replaced
# every update) — a durable, git-independent record of what's installed, readable even if the
# service is down. Distinct from GET /api/health's "version" field, which reports what's actually
# running right now.
cat > "$SILVERTASK_INSTALL_DIR/installed-version.json" <<EOF
{
  "version": "$NEW_VERSION",
  "gitCommit": "$NEW_COMMIT",
  "installedAtUtc": "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
}
EOF
chown "$SILVERTASK_SERVICE_USER:$SILVERTASK_SERVICE_USER" "$SILVERTASK_INSTALL_DIR/installed-version.json"

st_step "Running database migrations"
(
    cd "$SILVERTASK_SOURCE_DIR"
    st_load_env_file "$SILVERTASK_ENV_FILE"
    dotnet ef database update --project Silver-Task.Server --startup-project Silver-Task.Server >> "$SILVERTASK_LOG_FILE" 2>&1
) || st_fail "Migration failed during update." \
    "The previous application version is still installed at $PREVIOUS_PUBLISH_DIR but NOT currently running the service — restore it manually (see README 'Rollback procedure') and restore the pre-update backup taken above before retrying."
st_info "Migrations applied."

st_step "Restarting service"
systemctl restart "$SILVERTASK_SERVICE_NAME" || st_fail "Service failed to restart." "journalctl -u $SILVERTASK_SERVICE_NAME -n 100"

st_step "Health check"
# Checked directly against the app's fixed internal port (see deploy/silvertask.service's
# ASPNETCORE_URLS) rather than through nginx — this script has no reliable way to know which
# public domain/port install-debian.sh was originally configured with, and checking the app
# process itself directly is what actually confirms the update succeeded regardless of the
# reverse proxy's own state.
if ! st_health_check "http://127.0.0.1:5000" 15 3; then
    st_fail "Application did not become healthy after update." \
        "The new version is installed but not responding. Check: systemctl status $SILVERTASK_SERVICE_NAME, journalctl -u $SILVERTASK_SERVICE_NAME -n 100. Previous build kept at $PREVIOUS_PUBLISH_DIR for manual rollback."
fi

rm -rf "$PREVIOUS_PUBLISH_DIR"
st_info "=================================================================="
st_info " Update complete — now running $NEW_COMMIT"
st_info "=================================================================="
