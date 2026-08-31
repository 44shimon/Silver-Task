#!/bin/bash
# Silver Task — uninstall script.
#
# By default this ONLY stops/disables/removes the running service, systemd unit, and nginx
# site config. It NEVER touches the database, uploaded files, backups, or the environment
# file unless you explicitly pass --remove-data AND then separately confirm by typing an
# exact confirmation phrase — this is deliberately harder to do by accident than a plain
# `-y`/Enter shortcut, per the spec's own "must be extremely clear about destructive
# operations" requirement.
#
# Usage:
#   sudo ./scripts/uninstall-debian.sh                    # stop/remove services only, keep all data
#   sudo ./scripts/uninstall-debian.sh --remove-data       # also permanently delete the database,
#                                                           # uploaded files, backups, and .env
#                                                           # (prompts for explicit confirmation)
#   sudo ./scripts/uninstall-debian.sh --remove-data --force   # same, but non-interactive
#                                                                # (for scripted teardown of disposable
#                                                                # test/staging environments only —
#                                                                # never use --force on a system that
#                                                                # might hold real data)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

REMOVE_DATA=false
FORCE=false
for arg in "$@"; do
    case "$arg" in
        --remove-data) REMOVE_DATA=true ;;
        --force) FORCE=true ;;
        --help|-h)
            echo "Usage: sudo $0 [--remove-data [--force]]"
            echo "Without --remove-data: stops and removes services only. Database, uploaded files,"
            echo "backups, and the environment file are always preserved unless --remove-data is given."
            exit 0
            ;;
        *) st_fail "Unknown argument: $arg" "Run with --help for usage." ;;
    esac
done

st_require_root "$@"

st_step "Stopping and disabling the Silver Task service"
if systemctl list-unit-files "${SILVERTASK_SERVICE_NAME}.service" >/dev/null 2>&1; then
    systemctl stop "$SILVERTASK_SERVICE_NAME" 2>/dev/null || true
    systemctl disable "$SILVERTASK_SERVICE_NAME" 2>/dev/null || true
    rm -f "/etc/systemd/system/${SILVERTASK_SERVICE_NAME}.service"
    systemctl daemon-reload
    st_info "Service stopped, disabled, and unit file removed."
else
    st_warn "No $SILVERTASK_SERVICE_NAME systemd service found — nothing to stop."
fi

st_step "Removing nginx site configuration"
if [ -f /etc/nginx/sites-enabled/silvertask ] || [ -f /etc/nginx/sites-available/silvertask ]; then
    rm -f /etc/nginx/sites-enabled/silvertask /etc/nginx/sites-available/silvertask
    nginx -t >> "$SILVERTASK_LOG_FILE" 2>&1 && systemctl reload nginx || st_warn "nginx reload after removing the site config failed — check nginx -t manually."
    st_info "nginx site configuration removed."
else
    st_warn "No nginx site configuration found — nothing to remove."
fi

st_info "PostgreSQL and nginx themselves were left installed (they may be used by other applications on this host) — only Silver Task's own service/config was removed."

st_step "Removing application build/source (NOT your data)"
rm -rf "$SILVERTASK_PUBLISH_DIR" "$SILVERTASK_SOURCE_DIR"
st_info "Removed $SILVERTASK_PUBLISH_DIR and $SILVERTASK_SOURCE_DIR."

# Read the storage location (if the env file still exists) up front so it's available to both
# the "preserved" summary below and the --remove-data deletion path further down — avoids
# referencing an unset variable under `set -u` in the non-destructive branch.
STORAGE_ROOT="/var/lib/silver-task/attachments"
if [ -f "$SILVERTASK_ENV_FILE" ]; then
    set -a
    # shellcheck disable=SC1090
    source "$SILVERTASK_ENV_FILE"
    set +a
    STORAGE_ROOT="${Attachments__StorageRoot:-$STORAGE_ROOT}"
fi

echo
if [ "$REMOVE_DATA" = false ]; then
    st_info "=================================================================="
    st_info " Silver Task application/service removed."
    st_info " PRESERVED (not deleted): database, uploaded files ($STORAGE_ROOT default"
    st_info " /var/lib/silver-task/attachments), backups ($SILVERTASK_BACKUP_DIR),"
    st_info " and $SILVERTASK_ENV_FILE."
    st_info ""
    st_info " To permanently delete this data too, re-run:"
    st_info "   sudo $0 --remove-data"
    st_info "=================================================================="
    exit 0
fi

# --- Destructive path — requires explicit, hard-to-accidentally-trigger confirmation ---
echo
st_warn "!! --remove-data was specified. This will PERMANENTLY AND IRREVERSIBLY delete: !!"
st_warn "   - The PostgreSQL database and its user role"
st_warn "   - All uploaded files"
st_warn "   - All backups in $SILVERTASK_BACKUP_DIR"
st_warn "   - The environment file ($SILVERTASK_ENV_FILE, including its secrets)"
st_warn ""
st_warn "There is NO undo for this beyond a backup taken before now."
echo

if [ "$FORCE" = false ]; then
    if ! st_confirm_destructive "Permanently delete all Silver Task data?" "DELETE"; then
        st_info "Cancelled — no data was deleted. Services remain uninstalled from the earlier step."
        exit 0
    fi
else
    st_warn "--force given: skipping the interactive confirmation prompt. Proceeding with permanent deletion in 5 seconds — Ctrl+C now to abort."
    sleep 5
fi

st_step "Deleting database"
if [ -n "${ConnectionStrings__DefaultConnection:-}" ]; then
    DB_NAME=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Database=)[^;]*' || true)
    DB_USER=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Username=)[^;]*' || true)
    if [ -n "${DB_NAME:-}" ]; then
        st_run_as_postgres psql -c "DROP DATABASE IF EXISTS $DB_NAME;" >> "$SILVERTASK_LOG_FILE" 2>&1 || st_warn "Could not drop database $DB_NAME — it may not exist or PostgreSQL may not be running."
        st_info "Database $DB_NAME dropped."
    fi
    if [ -n "${DB_USER:-}" ]; then
        st_run_as_postgres psql -c "DROP ROLE IF EXISTS $DB_USER;" >> "$SILVERTASK_LOG_FILE" 2>&1 || st_warn "Could not drop role $DB_USER."
        st_info "Database role $DB_USER dropped."
    fi
else
    st_warn "No database connection string found (env file already missing?) — cannot determine database name to drop. Remove it manually if needed: runuser -u postgres -- psql -l"
fi

st_step "Deleting uploaded files"
if [ -d "$STORAGE_ROOT" ]; then
    rm -rf "$STORAGE_ROOT"
    st_info "Deleted $STORAGE_ROOT."
fi

st_step "Deleting backups"
if [ -d "$SILVERTASK_BACKUP_DIR" ]; then
    rm -rf "$SILVERTASK_BACKUP_DIR"
    st_info "Deleted $SILVERTASK_BACKUP_DIR."
fi

st_step "Deleting environment file and remaining install directory"
rm -f "$SILVERTASK_ENV_FILE"
rm -rf "$SILVERTASK_ENV_DIR" "$SILVERTASK_INSTALL_DIR"
st_info "Deleted $SILVERTASK_ENV_FILE and $SILVERTASK_INSTALL_DIR."

if id "$SILVERTASK_SERVICE_USER" >/dev/null 2>&1; then
    userdel "$SILVERTASK_SERVICE_USER" 2>/dev/null || true
    st_info "Removed service account $SILVERTASK_SERVICE_USER."
fi

st_info "=================================================================="
st_info " Silver Task fully uninstalled — all application data permanently deleted."
st_info "=================================================================="
