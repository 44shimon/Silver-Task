#!/bin/bash
# Silver Task — backup script.
#
# Backs up the three things needed to fully restore an installation: the PostgreSQL database
# (pg_dump, custom format — the same tool/format verified by hand in Phase 47's real
# backup/restore test), the uploaded-file storage directory, and the environment file
# (permissions preserved, so a restored copy is still root-readable-only). Safe to run
# repeatedly — never modifies the running installation, only reads from it.
#
# Phase 53 additions (all backward-compatible — a bare `sudo ./scripts/backup-debian.sh`, e.g. the
# cron job README documents, behaves exactly as before plus the additive manifest below):
#   - manifest.json, written incrementally as each stage completes, so even a mid-script failure
#     leaves a record of exactly how far it got (used by scripts/lib/upgrade.sh's
#     st_up_run_backup, which reads this file to tell a failed database backup apart from a failed
#     database *verification*, etc. — see README "Upgrade Engine").
#   - Real structural verification (pg_restore --list for the database dump, required-key presence
#     for the configuration copy), not just "the file is non-empty."
#   - Retention now protects the single newest backup and any backup linked to an in-progress
#     upgrade (see st_assert_safe_backup_dir/st_is_backup_set_name in lib/common.sh), and only
#     ever deletes directories matching this script's own YYYYMMDD-HHMMSS naming.
#
# Usage:
#   sudo ./scripts/backup-debian.sh                            # backs up to /var/backups/silver-task
#   sudo ./scripts/backup-debian.sh --keep=14                   # retain the last 14 backups (default: 7)
#   sudo ./scripts/backup-debian.sh --max-age-days=30           # also delete anything older than 30 days
#   sudo ./scripts/backup-debian.sh --tag=pre-upgrade --upgrade-id=upgrade-... \
#       --installed-version=1.0.1 --target-version=1.1.0        # used internally by the upgrade engine

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

KEEP=7
MAX_AGE_DAYS=0
TAG="manual"
UPGRADE_ID=""
INSTALLED_VERSION=""
TARGET_VERSION=""
for arg in "$@"; do
    case "$arg" in
        --keep=*) KEEP="${arg#*=}" ;;
        --max-age-days=*) MAX_AGE_DAYS="${arg#*=}" ;;
        --tag=*) TAG="${arg#*=}" ;;
        --upgrade-id=*) UPGRADE_ID="${arg#*=}" ;;
        --installed-version=*) INSTALLED_VERSION="${arg#*=}" ;;
        --target-version=*) TARGET_VERSION="${arg#*=}" ;;
        --help|-h)
            echo "Usage: sudo $0 [--keep=<count>] [--max-age-days=<days>] [--tag=<type>] [--upgrade-id=<id>] [--installed-version=<v>] [--target-version=<v>]"
            exit 0
            ;;
        *) st_fail "Unknown argument: $arg" "Run with --help for usage." ;;
    esac
done
case "$MAX_AGE_DAYS" in
    ''|*[!0-9]*) st_fail "Invalid --max-age-days value \"$MAX_AGE_DAYS\" (must be a non-negative integer)." ;;
esac

st_require_root "$@"

if ! st_is_installed; then
    st_fail "Silver Task does not appear to be installed at $SILVERTASK_INSTALL_DIR." "Nothing to back up."
fi

TIMESTAMP="$(date -u '+%Y%m%d-%H%M%S')"
BACKUP_SET_DIR="$SILVERTASK_BACKUP_DIR/$TIMESTAMP"
mkdir -p "$BACKUP_SET_DIR"
chmod 700 "$SILVERTASK_BACKUP_DIR" "$BACKUP_SET_DIR"
# Unprefixed and first, deliberately — scripts/lib/upgrade.sh's st_up_run_backup greps this exact
# line to find the manifest afterward, regardless of whether this script goes on to succeed or fail.
echo "BACKUP_SET_DIR=$BACKUP_SET_DIR"

DB_BACKUP_PATH=""
DB_BACKUP_VERIFIED=false
ATTACHMENTS_BACKUP_PATH=""
CONFIG_BACKUP_PATH=""
CONFIG_BACKUP_VERIFIED=false

write_manifest() {
    cat > "$BACKUP_SET_DIR/manifest.json" <<EOF
{
  "type": "$(st_json_escape "$TAG")",
  "createdAt": "$(date -u '+%Y-%m-%dT%H:%M:%SZ')",
  "upgradeId": "$(st_json_escape "$UPGRADE_ID")",
  "installedVersion": "$(st_json_escape "$INSTALLED_VERSION")",
  "targetVersion": "$(st_json_escape "$TARGET_VERSION")",
  "databaseBackup": "$(st_json_escape "$DB_BACKUP_PATH")",
  "databaseBackupVerified": $DB_BACKUP_VERIFIED,
  "attachmentsBackup": "$(st_json_escape "$ATTACHMENTS_BACKUP_PATH")",
  "configurationBackup": "$(st_json_escape "$CONFIG_BACKUP_PATH")",
  "configurationBackupVerified": $CONFIG_BACKUP_VERIFIED
}
EOF
    chmod 600 "$BACKUP_SET_DIR/manifest.json"
}
write_manifest

# --- Read connection details from the env file without ever echoing/logging the password ---
st_load_env_file "$SILVERTASK_ENV_FILE"
DB_HOST=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Host=)[^;]*')
DB_NAME=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Database=)[^;]*')
DB_USER=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Username=)[^;]*')
DB_PASS=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Password=)[^;]*')
STORAGE_ROOT="${Attachments__StorageRoot:-/var/lib/silver-task/attachments}"

st_step "Backing up database ($DB_NAME)"
PGPASSWORD="$DB_PASS" pg_dump -h "${DB_HOST:-localhost}" -U "$DB_USER" -d "$DB_NAME" -F c -f "$BACKUP_SET_DIR/database.dump" \
    || st_fail "DATABASE BACKUP FAILED." "Check that PostgreSQL is running (systemctl status postgresql) and credentials in $SILVERTASK_ENV_FILE are correct."
if [ ! -s "$BACKUP_SET_DIR/database.dump" ]; then
    st_fail "DATABASE BACKUP FAILED — produced an empty file, treating this as a failure, not a successful empty backup."
fi
DB_BACKUP_PATH="database.dump"
write_manifest
DB_BACKUP_SIZE="$(du -h "$BACKUP_SET_DIR/database.dump" | cut -f1)"
st_info "Database backup created: $DB_BACKUP_SIZE (never logs the credentials used)."

st_step "Verifying database backup"
# A genuine structural read of the custom-format dump's table of contents — catches a truncated
# or corrupt dump that would still pass the "non-empty file" check above.
if ! pg_restore --list "$BACKUP_SET_DIR/database.dump" >> "$SILVERTASK_LOG_FILE" 2>&1; then
    st_fail "DATABASE BACKUP VERIFICATION FAILED — pg_restore could not read the backup's table of contents." \
        "See $SILVERTASK_LOG_FILE. The backup file may be truncated or corrupt; do not rely on it."
fi
DB_BACKUP_VERIFIED=true
write_manifest
st_info "Database backup verified (structurally valid — $DB_BACKUP_SIZE)."

st_step "Backing up file storage ($STORAGE_ROOT)"
if [ -d "$STORAGE_ROOT" ]; then
    tar -czf "$BACKUP_SET_DIR/attachments.tar.gz" -C "$(dirname "$STORAGE_ROOT")" "$(basename "$STORAGE_ROOT")" \
        || st_fail "File storage backup failed."
    ATTACHMENTS_BACKUP_PATH="attachments.tar.gz"
    write_manifest
    FILES_BACKUP_SIZE="$(du -h "$BACKUP_SET_DIR/attachments.tar.gz" | cut -f1)"
    st_info "File storage backup verified: $FILES_BACKUP_SIZE."
else
    st_warn "Storage directory $STORAGE_ROOT does not exist — nothing to back up (no attachments uploaded yet)."
fi

st_step "Backing up configuration"
cp "$SILVERTASK_ENV_FILE" "$BACKUP_SET_DIR/silvertask.env"
chmod 600 "$BACKUP_SET_DIR/silvertask.env"
CONFIG_BACKUP_PATH="silvertask.env"
write_manifest
st_info "Configuration backup saved (permissions restricted to root, contains secrets)."

st_step "Verifying configuration backup"
if [ ! -s "$BACKUP_SET_DIR/silvertask.env" ]; then
    st_fail "CONFIGURATION BACKUP VERIFICATION FAILED — backed-up file is empty."
fi
for key in ConnectionStrings__DefaultConnection Jwt__Secret; do
    if ! grep -q "^${key}=" "$BACKUP_SET_DIR/silvertask.env"; then
        st_fail "CONFIGURATION BACKUP VERIFICATION FAILED — expected key \"$key\" not found." \
            "The environment file may be incomplete. Values themselves are never printed."
    fi
done
CONFIG_BACKUP_VERIFIED=true
write_manifest
st_info "Configuration backup verified (required keys present — values never logged)."

# --- Retention ---
st_step "Applying retention (keep last $KEEP, max age ${MAX_AGE_DAYS}d${MAX_AGE_DAYS:+, 0 = unlimited})"
st_assert_safe_backup_dir "$SILVERTASK_BACKUP_DIR"

# An upgrade that's staged/READY_FOR_ACTIVATION (anything other than FAILED) still needs its own
# pre-upgrade backup available for the eventual activation step — never delete it out from under
# an in-progress upgrade just because it happens to be the oldest.
PROTECTED_UPGRADE_ID=""
if [ -f "$SILVERTASK_UPGRADE_STATE_FILE" ]; then
    STATE_STATUS="$(sed -n 's/.*"status"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$SILVERTASK_UPGRADE_STATE_FILE" | head -1)"
    if [ "$STATE_STATUS" != "FAILED" ]; then
        PROTECTED_UPGRADE_ID="$(sed -n 's/.*"upgradeId"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$SILVERTASK_UPGRADE_STATE_FILE" | head -1)"
    fi
fi

mapfile -t candidate_dirs < <(find "$SILVERTASK_BACKUP_DIR" -maxdepth 1 -mindepth 1 -type d -printf '%f\n' 2>/dev/null | sort)
newest=""
total_recognized=0
for name in "${candidate_dirs[@]}"; do
    if st_is_backup_set_name "$name"; then
        total_recognized=$((total_recognized + 1))
        newest="$name"
    else
        st_warn "Retention: leaving unrecognized entry alone (not a backup this script created): $name"
    fi
done

to_remove_by_count=0
[ "$total_recognized" -gt "$KEEP" ] && to_remove_by_count=$((total_recognized - KEEP))

now_epoch="$(date -u +%s)"
removed=0
for name in "${candidate_dirs[@]}"; do
    st_is_backup_set_name "$name" || continue
    [ "$name" = "$newest" ] && continue

    manifest="$SILVERTASK_BACKUP_DIR/$name/manifest.json"
    if [ -n "$PROTECTED_UPGRADE_ID" ] && [ -f "$manifest" ]; then
        entry_upgrade_id="$(sed -n 's/.*"upgradeId"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$manifest" | head -1)"
        if [ "$entry_upgrade_id" = "$PROTECTED_UPGRADE_ID" ]; then
            st_info "Retention: protecting $name (linked to in-progress upgrade $PROTECTED_UPGRADE_ID)."
            continue
        fi
    fi

    delete_this=false
    if [ "$removed" -lt "$to_remove_by_count" ]; then
        delete_this=true
    elif [ "$MAX_AGE_DAYS" -gt 0 ]; then
        dir_epoch="$(date -u -d "${name:0:4}-${name:4:2}-${name:6:2} ${name:9:2}:${name:11:2}:${name:13:2}" +%s 2>/dev/null || echo "$now_epoch")"
        age_days=$(( (now_epoch - dir_epoch) / 86400 ))
        [ "$age_days" -ge "$MAX_AGE_DAYS" ] && delete_this=true
    fi

    if [ "$delete_this" = true ]; then
        st_info "Removing old backup: $SILVERTASK_BACKUP_DIR/$name"
        rm -rf "${SILVERTASK_BACKUP_DIR:?}/$name"
        removed=$((removed + 1))
    fi
done

st_info "=================================================================="
st_info " Backup complete: $BACKUP_SET_DIR"
st_info " Database:      $DB_BACKUP_SIZE (verified)"
st_info " File storage:  ${FILES_BACKUP_SIZE:-none}"
st_info " Configuration: verified"
st_info " Retained backups: $(find "$SILVERTASK_BACKUP_DIR" -maxdepth 1 -mindepth 1 -type d | wc -l) (keeping last $KEEP)"
st_info "=================================================================="
