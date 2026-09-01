#!/bin/bash
# Silver Task — Debian update / upgrade-engine script.
#
# Two distinct things live in this one file:
#
#   1. The LEGACY full-update path (no upgrade-engine flags — just today's usage, optionally with
#      --skip-backup/--ref=) — UNCHANGED since Phase 51: backs up, fetches/checks out the latest
#      source in place, rebuilds, migrates, restarts, and health-checks. This is what actually
#      deploys a change; Phase 52 does not alter a single line of its behavior.
#   2. The Phase 52 upgrade ENGINE (--check/--status/--latest/--target-version, optionally with
#      --dry-run/--yes) — new: discovers, validates, and stages a target release WITHOUT
#      activating it (no backup, no migration, no restart, no change to installed-version.json).
#      Its job is to PREPARE a release for a future ACTIVATE step (Phase 53+); it deliberately
#      never performs one itself. See README "Upgrade Engine" for the full explanation.
#
# Usage:
#   sudo ./scripts/update-debian.sh                                # legacy: update to latest + activate
#   sudo ./scripts/update-debian.sh --skip-backup                  # legacy, skip the pre-update backup
#   sudo ./scripts/update-debian.sh --ref=v1.0.1                   # legacy, update to a specific tag/branch
#   sudo ./scripts/update-debian.sh --check                        # is a newer stable release available?
#   sudo ./scripts/update-debian.sh --status                       # upgrade/version status report
#   sudo ./scripts/update-debian.sh --latest                       # prepare (not activate) the latest stable release
#   sudo ./scripts/update-debian.sh --target-version 1.1.0         # prepare (not activate) a specific release
#   sudo ./scripts/update-debian.sh --dry-run --latest             # show what --latest would do, change nothing
#   sudo ./scripts/update-debian.sh --target-version 1.1.0 --yes   # skip the confirmation prompt
#   sudo ./scripts/update-debian.sh --help
#
# Exit codes (upgrade-engine modes: --check/--status/--latest/--target-version; the legacy path
# always used a plain 0 = success / 1 = failure and still does):
#   0 success / no blocking problem        4 target version unavailable   7 repository access failure
#   1 general error                        5 unsupported upgrade path     8 insufficient disk space
#   2 invalid arguments                    6 upgrade already in progress
#   3 version inconsistency

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

Upgrade engine (Phase 52 — PREPARES a release; never activates one):
  --check                   Report whether a newer stable release is available. Read-only.
  --status                  Report installed/running version and upgrade-lock status. Read-only.
  --latest                  Validate and stage the latest stable release.
  --target-version X.Y.Z    Validate and stage a specific stable release.
  --dry-run                 With --latest/--target-version: report only, change nothing.
  --yes                     With --latest/--target-version: skip the confirmation prompt.

  --help, -h                Show this message.

--check and --status never modify anything. --latest/--target-version (without --dry-run) fetch
and stage the release into an isolated git worktree and record upgrade lock/state/log files, but
never replace the running application, run migrations, or restart services — see README "Upgrade
Engine" for the full prepare-vs-activate explanation.
EOF
}

# --- Argument parsing ---
MODE="legacy"              # legacy | check | status | prepare | help
TARGET_SELECTOR=""         # "" | "latest" | "target-version"
TARGET_VERSION=""
DRY_RUN=false
ASSUME_YES=false
SKIP_BACKUP=false
REF=""

st_up_require_mode_legacy() {
    if [ "$MODE" != "legacy" ]; then
        st_up_usage >&2
        echo "ERROR: --check, --status, --latest, and --target-version cannot be combined with each other." >&2
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
if [ "$ASSUME_YES" = true ] && [ "$MODE" != "prepare" ]; then
    st_up_usage >&2
    echo "ERROR: --yes requires --latest or --target-version." >&2
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

    if [ "$lock_active" = true ]; then
        echo "Upgrade Status: IN PROGRESS"
        echo ""
        echo "Target Version: ${ST_UP_LOCK_TARGET:-unknown}"
        echo "Started: ${ST_UP_LOCK_STARTED:-unknown}"
    else
        declare -A state=()
        local state_output=""
        if state_output="$(st_up_state_read 2>/dev/null)"; then
            while IFS='=' read -r key value; do
                [ -n "$key" ] && state["$key"]="$value"
            done <<< "$state_output"
        fi
        case "${state[status]:-}" in
            CHECKING|VALIDATING|PREPARING|UPGRADE_IN_PROGRESS)
                echo "Upgrade Status: STALE UPGRADE LOCK DETECTED"
                echo ""
                echo "A previous upgrade preparation did not finish cleanly:"
                echo "  Target Version: ${state[targetVersion]:-unknown}"
                echo "  Started: ${state[startTimeUtc]:-unknown}"
                echo "  Last step: ${state[currentStep]:-unknown}"
                echo ""
                echo "No process currently holds the upgrade lock — it is safe to retry with --latest"
                echo "or --target-version. The interrupted attempt made no changes to the running"
                echo "application, database, or installed version (Phase 52 only ever prepares)."
                ;;
            *)
                echo "Upgrade Status: IDLE"
                if [ -n "${state[status]:-}" ]; then
                    echo ""
                    echo "Last attempt: ${state[status]} (target ${state[targetVersion]:-unknown}, ${state[lastUpdatedUtc]:-unknown})"
                fi
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
        echo "Disk space: ${ST_UP_DISK_AVAILABLE_MB}MB available, ~${ST_UP_DISK_REQUIRED_MB}MB estimated needed"
        echo "Preparation plan: fetch tag v$target, stage into an isolated git worktree under"
        echo "  $SILVERTASK_UPGRADE_STAGING_DIR/$target, record upgrade lock/state/log — the running"
        echo "  application, database, and installed version would NOT be touched."
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

    local start_time
    start_time="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
    st_up_state_write "PREPARING" "$installed" "$target" "staging release" "$start_time"
    st_up_log "prepare: lock acquired, staging $target"

    st_step "Fetching and staging release $target"
    if ! st_up_prepare_worktree "$SILVERTASK_SOURCE_DIR" "$target" "$SILVERTASK_UPGRADE_STAGING_DIR"; then
        st_up_state_write "FAILED" "$installed" "$target" "staging release" "$start_time"
        st_up_log "prepare: FAILED, could not stage worktree for $target"
        st_fail "Could not fetch/stage release $target." "Check $SILVERTASK_UPGRADE_LOG_FILE and that $SILVERTASK_REPO_URL is reachable."
    fi
    st_info "Staged at $ST_UP_STAGED_DIR."

    st_step "Verifying staged release"
    if ! "$SCRIPT_DIR/check-version.sh" "$ST_UP_STAGED_DIR" > /dev/null; then
        st_up_cleanup_worktree "$SILVERTASK_SOURCE_DIR" "$ST_UP_STAGED_DIR"
        st_up_state_write "FAILED" "$installed" "$target" "verifying staged release" "$start_time"
        st_up_log "prepare: FAILED, staged release $target failed check-version.sh"
        st_fail "Staged release $target failed its own version/tag consistency check." "See $SILVERTASK_UPGRADE_LOG_FILE."
    fi

    st_up_state_write "COMPLETED" "$installed" "$target" "prepared" "$start_time"
    st_up_log "prepare: COMPLETED (preparation only), $installed -> $target staged at $ST_UP_STAGED_DIR"
    st_info "=================================================================="
    st_info " Preparation complete — release $target staged, NOT activated."
    st_info " Staged at: $ST_UP_STAGED_DIR"
    st_info " The running application is still on $installed. Activation (backup, build, migrate,"
    st_info " restart) is a separate, future step — see README 'Upgrade Engine'."
    st_info "=================================================================="
    exit 0
}

case "$MODE" in
    check) cmd_check ;;
    status) cmd_status ;;
    prepare) cmd_prepare ;;
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
