#!/bin/bash
# Silver Task — Debian update script.
#
# Updates an existing installation (see scripts/install-debian.sh) to the latest committed
# revision: backs up first (via scripts/backup-debian.sh — refuses to continue if the backup
# fails), pulls the latest source, rebuilds, runs migrations, restarts the service, and
# health-checks the result. Never touches the database or uploaded files destructively, and
# never overwrites the existing environment file.
#
# Usage:
#   sudo ./scripts/update-debian.sh
#   sudo ./scripts/update-debian.sh --skip-backup     # not recommended — see "Update safety" in README
#   sudo ./scripts/update-debian.sh --ref=v1.0.1      # update to a specific tag/branch instead of the default branch

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

SKIP_BACKUP=false
REF=""
for arg in "$@"; do
    case "$arg" in
        --skip-backup) SKIP_BACKUP=true ;;
        --ref=*) REF="${arg#*=}" ;;
        --help|-h)
            echo "Usage: sudo $0 [--skip-backup] [--ref=<git-ref>]"
            exit 0
            ;;
        *) st_fail "Unknown argument: $arg" "Run with --help for usage." ;;
    esac
done

st_require_root "$@"

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
