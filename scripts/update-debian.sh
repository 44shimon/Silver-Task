#!/bin/bash
# Silver Task — Debian update / upgrade-engine script.
#
# Two distinct things live in this one file:
#
#   1. The LEGACY full-update path (no upgrade-engine flags — just today's usage, optionally with
#      --skip-backup/--ref=) — UNCHANGED since Phase 51: backs up, fetches/checks out the latest
#      source in place, rebuilds, migrates, restarts, and health-checks. This is what actually
#      deploys a change; Phase 52 does not alter a single line of its behavior.
#   2. The upgrade ENGINE (--check/--status/--latest/--target-version/--activate, optionally with
#      --dry-run/--yes) — Phase 52 built discovery/validation/staging; Phase 53 added the safety
#      layer that must exist before anything can activate: a verified pre-upgrade backup
#      (database + attachments + configuration, via scripts/backup-debian.sh), a persistent-data
#      placement check, and migration discovery/validation/planning via the project's own EF Core
#      CLI — never executing a migration. A successful --latest/--target-version run ends in
#      READY_FOR_ACTIVATION, still without activating anything. Phase 54's --activate is what
#      actually does: maintenance mode, an atomic-as-practical release swap, running the real
#      migration, restarting the service, health/version validation, and only then committing
#      installed-version.json. See README "Upgrade Engine" for the full explanation.
#
# Usage:
#   sudo ./scripts/update-debian.sh                                # legacy: update to latest + activate
#   sudo ./scripts/update-debian.sh --skip-backup                  # legacy, skip the pre-update backup
#   sudo ./scripts/update-debian.sh --ref=v1.0.1                   # legacy, update to a specific tag/branch
#   sudo ./scripts/update-debian.sh --check                        # is a newer stable release available?
#   sudo ./scripts/update-debian.sh --status                       # upgrade/version status report
#   sudo ./scripts/update-debian.sh --latest                       # validate, back up, and stage the latest stable release
#   sudo ./scripts/update-debian.sh --target-version 1.1.0         # validate, back up, and stage a specific release
#   sudo ./scripts/update-debian.sh --dry-run --latest             # show what --latest would do, change nothing
#   sudo ./scripts/update-debian.sh --target-version 1.1.0 --yes   # skip the confirmation prompt
#   sudo ./scripts/update-debian.sh --activate                     # activate a prepared, READY_FOR_ACTIVATION release
#   sudo ./scripts/update-debian.sh --activate --yes                # activate without the confirmation prompt
#   sudo ./scripts/update-debian.sh --help
#
# Exit codes (upgrade-engine modes: --check/--status/--latest/--target-version/--activate; the
# legacy path always used a plain 0 = success / 1 = failure and still does):
#   0 success / no blocking problem        9 database backup failed        18 maintenance mode failure
#   1 general error                       10 database backup verify failed 19 application activation failure
#   2 invalid arguments                   11 configuration backup failed   20 service startup failure
#   3 version inconsistency               12 config backup verification failed 21 migration execution failure
#   4 target version unavailable          13 persistent data safety check failed 22 health check timeout
#   5 unsupported upgrade path            14 current migration state invalid 23 version validation failure
#   6 upgrade already in progress         15 target migration validation failed 24 smoke test failure
#   7 repository access failure           16 migration planning failed     25 interrupted upgrade detected (--status)
#   8 insufficient disk space             17 activation prerequisites missing

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"
# shellcheck source=lib/upgrade.sh
source "$SCRIPT_DIR/lib/upgrade.sh"

st_up_usage() {
    cat <<'EOF'
Usage: sudo ./scripts/update-debian.sh [command] [options]

Legacy full update (unchanged since Phase 51 — actually deploys a change):
  (no command)              Update to the latest default-branch commit and activate it.
  --ref=<git-ref>           Update to a specific tag/branch instead, and activate it.
  --skip-backup             Skip the pre-update backup (not recommended).

Upgrade engine — prepare (--latest/--target-version) then activate (--activate) are separate,
deliberately confirmed steps:
  --check                   Report whether a newer stable release is available. Read-only.
  --status                  Report installed/running version and upgrade-lock status. Read-only.
  --latest                  Validate, back up, and stage the latest stable release.
  --target-version X.Y.Z    Validate, back up, and stage a specific stable release.
  --dry-run                 With --latest/--target-version: report only, change nothing.
  --activate                Activate a prepared (READY_FOR_ACTIVATION) release. See below.
  --yes                     With --latest/--target-version/--activate: skip the confirmation prompt.

  --help, -h                Show this message.

--check and --status never modify anything. --latest/--target-version (without --dry-run) stage
the release into an isolated git worktree, create and verify a pre-upgrade backup (database +
attachments + configuration), check persistent-data placement, and validate/plan any required
migrations — but never replace the running application, run a migration, or restart services.

--activate requires a release already READY_FOR_ACTIVATION (i.e. a prior --latest/--target-version
succeeded and nothing has changed since). It enables maintenance mode, swaps in the prepared
release, runs any required migration, restarts the service, validates health/version, and only
then commits the installed version and disables maintenance mode. See README "Upgrade Engine" for
the full prepare-vs-activate explanation and docs/upgrade-activation.md for the full workflow.
EOF
}

# --- Argument parsing ---
MODE="legacy"              # legacy | check | status | prepare | activate | help
TARGET_SELECTOR=""         # "" | "latest" | "target-version"
TARGET_VERSION=""
DRY_RUN=false
ASSUME_YES=false
SKIP_BACKUP=false
REF=""

st_up_require_mode_legacy() {
    if [ "$MODE" != "legacy" ]; then
        st_up_usage >&2
        echo "ERROR: --check, --status, --latest, --target-version, and --activate cannot be combined with each other." >&2
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
        --yes) ASSUME_YES=true ;;
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
if [ "$DRY_RUN" = true ] && [ "$MODE" != "prepare" ]; then
    st_up_usage >&2
    echo "ERROR: --dry-run requires --latest or --target-version." >&2
    exit 2
fi
if [ "$ASSUME_YES" = true ] && [ "$MODE" != "prepare" ] && [ "$MODE" != "activate" ]; then
    st_up_usage >&2
    echo "ERROR: --yes requires --latest, --target-version, or --activate." >&2
    exit 2
fi
if [ "$MODE" = "prepare" ] && [ "$TARGET_SELECTOR" = "target-version" ]; then
    if [ -z "$TARGET_VERSION" ] || ! st_up_semver_valid "$TARGET_VERSION"; then
        st_up_usage >&2
        echo "ERROR: --target-version \"$TARGET_VERSION\" is not a valid version (expected MAJOR.MINOR.PATCH, e.g. 1.1.0)." >&2
        exit 2
    fi
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

    if ! discovered="$(st_up_discover_stable_releases)"; then
        echo "Latest Stable Version: UNKNOWN"
        echo ""
        echo "Update Check: FAILED (could not reach $SILVERTASK_REPO_URL)"
        st_up_log "check: repository unreachable (installed=$installed)"
        exit 7
    fi
    if [ -z "$discovered" ]; then
        echo "Latest Stable Version: UNKNOWN"
        echo ""
        echo "Update Check: FAILED (no stable releases found on $SILVERTASK_REPO_URL)"
        st_up_log "check: no stable releases discovered (installed=$installed)"
        exit 7
    fi
    latest="$(printf '%s\n' "$discovered" | tail -1)"
    echo "Latest Stable Version: $latest"
    echo ""
    if [ "$installed" = "unknown" ]; then
        echo "Update Check: FAILED (installed version unknown — see README 'Version information')"
        st_up_log "check: installed version unknown (latest=$latest)"
        exit 3
    fi
    if [ "$(st_up_semver_compare "$installed" "$latest")" -lt 0 ]; then
        echo "UPDATE AVAILABLE"
        st_up_log "check: update available (installed=$installed latest=$latest)"
    else
        echo "YOU ARE UP TO DATE"
        st_up_log "check: up to date (installed=$installed latest=$latest)"
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

    local discovered latest
    if discovered="$(st_up_discover_stable_releases)" && [ -n "$discovered" ]; then
        latest="$(printf '%s\n' "$discovered" | tail -1)"
        echo "Latest Stable Version: $latest"
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
        echo "Latest Stable Version: UNKNOWN"
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
    st_up_maintenance_probe

    if [ "$lock_active" = true ]; then
        echo "Upgrade Status: IN PROGRESS"
        echo ""
        echo "Target Version: ${ST_UP_LOCK_TARGET:-unknown}"
        echo "Started: ${ST_UP_LOCK_STARTED:-unknown}"
        if [ "$ST_UP_MAINTENANCE_ACTIVE" = true ]; then
            echo "Maintenance Mode: ACTIVE"
        fi
        exit 0
    fi

    # Phase 54 — a maintenance flag left active with NO process holding the upgrade lock is
    # always a red flag (reboot, power loss, kill -9 mid-activation — brief's own §29 scenarios),
    # regardless of what upgrade-state.json's own status says. Checked before the state-based
    # cases below, and reported/exited distinctly (exit 25) since this is more urgent than a
    # merely-stale prepare attempt: the application may currently be unreachable.
    if [ "$ST_UP_MAINTENANCE_ACTIVE" = true ]; then
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
        echo "upgrade detection.\""
        exit 25
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

    st_step "Discovering stable releases"
    local discovered
    if ! discovered="$(st_up_discover_stable_releases)"; then
        st_error "Could not reach $SILVERTASK_REPO_URL to discover releases."
        st_up_log "prepare: aborted, repository unreachable"
        exit 7
    fi
    if [ -z "$discovered" ]; then
        st_error "No stable releases found on $SILVERTASK_REPO_URL."
        st_up_log "prepare: aborted, no stable releases discovered"
        exit 7
    fi

    local target
    if [ "$TARGET_SELECTOR" = "latest" ]; then
        target="$(printf '%s\n' "$discovered" | tail -1)"
        st_info "Latest stable release: $target"
    else
        target="$TARGET_VERSION"
        if ! printf '%s\n' "$discovered" | grep -qxF "$target"; then
            st_error "Target version \"$target\" is not an available stable release."
            st_error "Available stable releases: $(printf '%s\n' "$discovered" | tr '\n' ' ')"
            st_up_log "prepare: aborted, target $target unavailable"
            exit 4
        fi
    fi
    st_info "Target Version: $target"

    if [ "$target" = "$installed" ]; then
        st_info "ALREADY RUNNING TARGET VERSION"
        st_up_log "prepare: no-op, already on $target"
        exit 0
    fi
    if [ "$(st_up_semver_compare "$target" "$installed")" -lt 0 ]; then
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
        st_error "UPGRADE ALREADY IN PROGRESS (target ${ST_UP_LOCK_TARGET:-unknown}, started ${ST_UP_LOCK_STARTED:-unknown}, pid ${ST_UP_LOCK_PID:-unknown})."
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

    st_step "Acquiring upgrade lock"
    if ! st_up_lock_acquire "$target"; then
        st_up_lock_probe || true
        st_error "UPGRADE ALREADY IN PROGRESS (target ${ST_UP_LOCK_TARGET:-unknown}, started ${ST_UP_LOCK_STARTED:-unknown}, pid ${ST_UP_LOCK_PID:-unknown})."
        st_up_log "activate: aborted, lock held by pid ${ST_UP_LOCK_PID:-unknown}"
        exit 6
    fi
    # Same crash-safety guarantee as cmd_prepare's lock: flock auto-releases if this process dies;
    # the trap makes a clean exit (success or a handled failure below) release it immediately.
    trap 'st_up_lock_release' EXIT

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

case "$MODE" in
    check) cmd_check ;;
    status) cmd_status ;;
    prepare) cmd_prepare ;;
    activate) cmd_activate ;;
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
