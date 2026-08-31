#!/bin/bash
# Silver Task — backup script.
#
# Backs up the three things needed to fully restore an installation: the PostgreSQL database
# (pg_dump, custom format — the same tool/format verified by hand in Phase 47's real
# backup/restore test), the uploaded-file storage directory, and the environment file
# (permissions preserved, so a restored copy is still root-readable-only). Safe to run
# repeatedly — never modifies the running installation, only reads from it.
#
# Usage:
#   sudo ./scripts/backup-debian.sh                  # backs up to /var/backups/silver-task
#   sudo ./scripts/backup-debian.sh --keep=14         # retain the last 14 backups (default: 7)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

KEEP=7
for arg in "$@"; do
    case "$arg" in
        --keep=*) KEEP="${arg#*=}" ;;
        --help|-h) echo "Usage: sudo $0 [--keep=<count>]"; exit 0 ;;
        *) st_fail "Unknown argument: $arg" "Run with --help for usage." ;;
    esac
done

st_require_root "$@"

if ! st_is_installed; then
    st_fail "Silver Task does not appear to be installed at $SILVERTASK_INSTALL_DIR." "Nothing to back up."
fi

TIMESTAMP="$(date -u '+%Y%m%d-%H%M%S')"
BACKUP_SET_DIR="$SILVERTASK_BACKUP_DIR/$TIMESTAMP"
mkdir -p "$BACKUP_SET_DIR"
chmod 700 "$SILVERTASK_BACKUP_DIR" "$BACKUP_SET_DIR"

# --- Read connection details from the env file without ever echoing/logging the password ---
st_load_env_file "$SILVERTASK_ENV_FILE"
DB_HOST=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Host=)[^;]*')
DB_NAME=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Database=)[^;]*')
DB_USER=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Username=)[^;]*')
DB_PASS=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Password=)[^;]*')
STORAGE_ROOT="${Attachments__StorageRoot:-/var/lib/silver-task/attachments}"

st_step "Backing up database ($DB_NAME)"
PGPASSWORD="$DB_PASS" pg_dump -h "${DB_HOST:-localhost}" -U "$DB_USER" -d "$DB_NAME" -F c -f "$BACKUP_SET_DIR/database.dump" \
    || st_fail "Database backup failed." "Check that PostgreSQL is running (systemctl status postgresql) and credentials in $SILVERTASK_ENV_FILE are correct."
if [ ! -s "$BACKUP_SET_DIR/database.dump" ]; then
    st_fail "Database backup produced an empty file — treating this as a failure, not a successful empty backup."
fi
DB_BACKUP_SIZE="$(du -h "$BACKUP_SET_DIR/database.dump" | cut -f1)"
st_info "Database backup verified: $DB_BACKUP_SIZE (never logs the credentials used)."

st_step "Backing up file storage ($STORAGE_ROOT)"
if [ -d "$STORAGE_ROOT" ]; then
    tar -czf "$BACKUP_SET_DIR/attachments.tar.gz" -C "$(dirname "$STORAGE_ROOT")" "$(basename "$STORAGE_ROOT")" \
        || st_fail "File storage backup failed."
    FILES_BACKUP_SIZE="$(du -h "$BACKUP_SET_DIR/attachments.tar.gz" | cut -f1)"
    st_info "File storage backup verified: $FILES_BACKUP_SIZE."
else
    st_warn "Storage directory $STORAGE_ROOT does not exist — nothing to back up (no attachments uploaded yet)."
fi

st_step "Backing up configuration"
cp "$SILVERTASK_ENV_FILE" "$BACKUP_SET_DIR/silvertask.env"
chmod 600 "$BACKUP_SET_DIR/silvertask.env"
st_info "Configuration backup saved (permissions restricted to root, contains secrets)."

# --- Retention ---
st_step "Applying retention (keep last $KEEP)"
mapfile -t existing_backups < <(find "$SILVERTASK_BACKUP_DIR" -maxdepth 1 -mindepth 1 -type d | sort)
count="${#existing_backups[@]}"
if [ "$count" -gt "$KEEP" ]; then
    to_remove=$((count - KEEP))
    for ((i = 0; i < to_remove; i++)); do
        st_info "Removing old backup: ${existing_backups[$i]}"
        rm -rf "${existing_backups[$i]}"
    done
fi

st_info "=================================================================="
st_info " Backup complete: $BACKUP_SET_DIR"
st_info " Database:      $DB_BACKUP_SIZE"
st_info " File storage:  ${FILES_BACKUP_SIZE:-none}"
st_info " Retained backups: $(find "$SILVERTASK_BACKUP_DIR" -maxdepth 1 -mindepth 1 -type d | wc -l) (keeping last $KEEP)"
st_info "=================================================================="
