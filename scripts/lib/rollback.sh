#!/bin/bash
# Silver Task — Phase 55 upgrade recovery & rollback primitives, used by
# scripts/update-debian.sh's --rollback mode.
#
# Sourced after lib/common.sh and lib/upgrade.sh (relies on st_info/st_error/st_json_escape/
# st_load_env_file, the SILVERTASK_* location constants, and upgrade.sh's
# st_up_state_read/st_up_migration_current_state). Never sourced or executed directly.
#
# Deliberately its own file, not folded into upgrade.sh: rollback reads what prepare/activate
# already recorded (upgrade-state.json, the `.previous` publish directory, the pre-upgrade backup
# manifest) but has its own state machine, its own eligibility rules, and its own destructive
# database-restore capability — keeping it separate matches how upgrade.sh itself was already
# split out of common.sh for the same reason.
#
# No jq/external JSON library dependency, same convention as every other file in this project —
# every JSON file here has a small, fixed, single-line-per-field shape, parsed/produced with
# grep/sed.

# --- Rollback ID ---
st_rb_generate_rollback_id() {
    printf 'rollback-%s-%s\n' "$(date -u '+%Y%m%d-%H%M%S')" "$(openssl rand -hex 3)"
}

# --- Rollback state ---
#
# A small on-disk record of the most recent rollback attempt, read by --status — deliberately a
# separate file from SILVERTASK_UPGRADE_STATE_FILE (upgrade.sh), since --status must report "Last
# Upgrade" and "Last Rollback" as two independent facts, not one overwriting the other. Same
# crash-safe incremental-write pattern as st_up_state_write: callers rewrite this after every
# stage completes, not just once at the end.
st_rb_state_write() {
    local status="$1" related_upgrade_id="$2" previous_failed_version="$3" restored_version="$4" \
        reason="$5" current_step="$6" start_time="$7" rollback_id="$8"
    cat > "$SILVERTASK_ROLLBACK_STATE_FILE" <<EOF
{
  "rollbackId": "$(st_json_escape "$rollback_id")",
  "relatedUpgradeId": "$(st_json_escape "$related_upgrade_id")",
  "previousFailedVersion": "$(st_json_escape "$previous_failed_version")",
  "restoredVersion": "$(st_json_escape "$restored_version")",
  "reason": "$(st_json_escape "$reason")",
  "startTimeUtc": "$(st_json_escape "$start_time")",
  "completedAtUtc": "$(st_json_escape "${ST_RB_STATE_COMPLETED_AT:-}")",
  "currentStep": "$(st_json_escape "$current_step")",
  "status": "$(st_json_escape "$status")",
  "databaseRestoreDecision": "$(st_json_escape "${ST_RB_STATE_DB_DECISION:-PENDING}")",
  "databaseRestorePerformed": "$(st_json_escape "${ST_RB_STATE_DB_RESTORE_PERFORMED:-false}")",
  "configurationRestorePerformed": "$(st_json_escape "${ST_RB_STATE_CONFIG_RESTORE_PERFORMED:-false}")",
  "emergencyBackupDir": "$(st_json_escape "${ST_RB_STATE_EMERGENCY_BACKUP_DIR:-}")",
  "healthCheckStatus": "$(st_json_escape "${ST_RB_STATE_HEALTH_CHECK_STATUS:-PENDING}")",
  "versionValidationStatus": "$(st_json_escape "${ST_RB_STATE_VERSION_VALIDATION_STATUS:-PENDING}")",
  "smokeTestStatus": "$(st_json_escape "${ST_RB_STATE_SMOKE_TEST_STATUS:-PENDING}")",
  "lastUpdatedUtc": "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
}
EOF
    chmod 640 "$SILVERTASK_ROLLBACK_STATE_FILE" 2>/dev/null || true
}

# Prints each field as KEY=VALUE (one per line, safe for `while IFS== read`). Returns 1 (prints
# nothing) if no rollback has ever been recorded.
st_rb_state_read() {
    [ -f "$SILVERTASK_ROLLBACK_STATE_FILE" ] || return 1
    local field
    for field in rollbackId relatedUpgradeId previousFailedVersion restoredVersion reason \
        startTimeUtc completedAtUtc currentStep status databaseRestoreDecision \
        databaseRestorePerformed configurationRestorePerformed emergencyBackupDir \
        healthCheckStatus versionValidationStatus smokeTestStatus lastUpdatedUtc; do
        printf '%s=%s\n' "$field" \
            "$(sed -n "s/.*\"$field\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\\1/p" "$SILVERTASK_ROLLBACK_STATE_FILE" | head -1)"
    done
}

# --- Rollback eligibility ---
#
# Reads the *last upgrade attempt's* own record (upgrade-state.json, via upgrade.sh's
# st_up_state_read — never a second "release history" mechanism) and confirms there is actually
# something to roll back to. Deliberately NOT gated on st_up_version_consistency being CONSISTENT
# — a MISMATCH after a failed activation is exactly the scenario rollback exists for, unlike
# cmd_prepare which must refuse to touch an inconsistent installation.
#
# Populates ST_RB_TARGET_VERSION (what to roll back TO — the last attempt's "currentVersion",
# i.e. what was installed before it), ST_RB_FAILED_VERSION (what's being rolled back FROM — that
# attempt's "targetVersion"), ST_RB_RELATED_UPGRADE_ID, ST_RB_BACKUP_DIR,
# ST_RB_MIGRATION_REQUIRED. On failure, sets ST_RB_BLOCKED_REASON and ST_RB_TARGET_UNAVAILABLE
# (true only for the specific "no preserved previous release" case, so the caller can choose exit
# 27 instead of the general exit 26) and returns 1.
st_rb_eligibility_check() {
    ST_RB_BLOCKED_REASON=""
    ST_RB_TARGET_UNAVAILABLE=false
    ST_RB_TARGET_VERSION=""
    ST_RB_FAILED_VERSION=""
    ST_RB_RELATED_UPGRADE_ID=""
    ST_RB_BACKUP_DIR=""
    ST_RB_MIGRATION_REQUIRED="unknown"

    local state_output
    if ! state_output="$(st_up_state_read 2>/dev/null)"; then
        ST_RB_BLOCKED_REASON="No upgrade attempt has ever been recorded — nothing to roll back."
        return 1
    fi

    local -A state=()
    while IFS='=' read -r key value; do
        [ -n "$key" ] && state["$key"]="$value"
    done <<< "$state_output"

    ST_RB_TARGET_VERSION="${state[currentVersion]:-}"
    ST_RB_FAILED_VERSION="${state[targetVersion]:-}"
    ST_RB_RELATED_UPGRADE_ID="${state[upgradeId]:-}"
    ST_RB_BACKUP_DIR="${state[backupDir]:-}"
    ST_RB_MIGRATION_REQUIRED="${state[migrationRequired]:-unknown}"

    if [ -z "$ST_RB_TARGET_VERSION" ] || [ -z "$ST_RB_FAILED_VERSION" ]; then
        ST_RB_BLOCKED_REASON="No recorded upgrade attempt has both a previous and target version — nothing to roll back."
        return 1
    fi
    if [ "${state[activationStatus]:-}" != "OK" ]; then
        ST_RB_BLOCKED_REASON="The last upgrade attempt (target $ST_RB_FAILED_VERSION) never completed its release switch (activationStatus=${state[activationStatus]:-unknown}) — the running application was never changed, so there is nothing to roll back."
        return 1
    fi
    if [ ! -d "${SILVERTASK_PUBLISH_DIR}.previous" ]; then
        ST_RB_BLOCKED_REASON="No preserved previous release found at ${SILVERTASK_PUBLISH_DIR}.previous."
        ST_RB_TARGET_UNAVAILABLE=true
        return 1
    fi
    if [ -z "$ST_RB_BACKUP_DIR" ] || [ ! -f "$ST_RB_BACKUP_DIR/manifest.json" ]; then
        ST_RB_BLOCKED_REASON="The pre-upgrade backup for this attempt is missing (expected manifest at ${ST_RB_BACKUP_DIR:-unknown}/manifest.json) — cannot safely determine rollback safety without it."
        return 1
    fi
    return 0
}

# --- Database rollback decision ---
#
# Pure logic reusing the migrationRequired flag Phase 53/54 already records per upgrade attempt —
# never a new compatibility matrix, never a guess. "unknown" (missing/corrupted historical data)
# deliberately does NOT default to either safe option — it blocks with MANUAL_RECOVERY_REQUIRED,
# since guessing which way is wrong here is exactly what the brief prohibits.
st_rb_database_decision() {
    case "$ST_RB_MIGRATION_REQUIRED" in
        false) ST_RB_DB_DECISION="APPLICATION_ONLY_ROLLBACK" ;;
        true) ST_RB_DB_DECISION="DATABASE_RESTORE_REQUIRED" ;;
        *) ST_RB_DB_DECISION="MANUAL_RECOVERY_REQUIRED" ;;
    esac
}

# --- Database restore ---
#
# Mirrors scripts/backup-debian.sh's own pg_dump invocation exactly (same connection parsing, same
# TCP + PGPASSWORD auth as the application's own DB user) rather than requiring postgres-superuser
# access — the app's DB user already owns every object in its database (`CREATE DATABASE ... OWNER
# $DB_USER` in install-debian.sh), so it has full DDL rights to --clean --if-exists its own
# schema. `--no-owner` avoids any ALTER OWNER TO friction against a dump taken under the same
# user. Every value comes from the already-validated connection string / a backup_dir already
# confirmed to exist by st_rb_eligibility_check — never raw administrator input.
st_rb_restore_database() {
    local backup_dir="$1" env_file="$2"
    st_load_env_file "$env_file"
    local db_host db_name db_user db_pass
    db_host=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Host=)[^;]*')
    db_name=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Database=)[^;]*')
    db_user=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Username=)[^;]*')
    db_pass=$(echo "$ConnectionStrings__DefaultConnection" | grep -oP '(?<=Password=)[^;]*')
    PGPASSWORD="$db_pass" pg_restore --clean --if-exists --no-owner \
        -h "${db_host:-localhost}" -U "$db_user" -d "$db_name" "$backup_dir/database.dump"
}

# Proves the restored database is actually compatible with the rollback target's own code — reuses
# st_up_migration_current_state (upgrade.sh) as-is: a real, read-only connection confirming no
# migration is pending relative to $SILVERTASK_SOURCE_DIR (which by this point is already checked
# out to the rollback target's tag via st_rb_switch_release). No new validation mechanism.
st_rb_validate_database() {
    local env_file="$1"
    st_up_migration_current_state "$SILVERTASK_SOURCE_DIR" "$env_file"
}

# --- Configuration restore (opt-in via --restore-config; see update-debian.sh) ---
#
# Always emergency-copies the CURRENT configuration before overwriting it — brief's own "preserve
# before destructive restoration" rule applied to configuration too, not just the database.
# Restores only the single silvertask.env file from the backup, never anything else in the backup
# directory. Sets ST_RB_CONFIG_EMERGENCY_COPY to the saved path on success.
st_rb_restore_configuration() {
    local backup_dir="$1" env_file="$2"
    ST_RB_CONFIG_EMERGENCY_COPY=""
    [ -f "$backup_dir/silvertask.env" ] || return 1
    local emergency_copy
    emergency_copy="${env_file}.emergency-$(date -u '+%Y%m%d%H%M%S')"
    cp "$env_file" "$emergency_copy" 2>/dev/null || return 1
    chmod 600 "$emergency_copy" 2>/dev/null || true
    cp "$backup_dir/silvertask.env" "$env_file" || return 1
    chmod 640 "$env_file" 2>/dev/null || true
    chown "root:$SILVERTASK_SERVICE_USER" "$env_file" 2>/dev/null || true
    ST_RB_CONFIG_EMERGENCY_COPY="$emergency_copy"
    return 0
}

# --- Release switch ---
#
# The swap half of what cmd_activate does, deliberately without a build step: `.previous` is
# already a built, previously-activated artifact (it was the live release until the failed
# activation replaced it), so there is nothing to compile. Checks SILVERTASK_SOURCE_DIR out to the
# rollback target's own tag (same mechanism cmd_activate already uses for the forward direction),
# preserves the failed release at `${SILVERTASK_PUBLISH_DIR}.failed` (one slot, overwritten on each
# rollback, mirroring the one `.previous` slot) with a small sidecar recording what it was and why
# it's there — never silently deleted, per the brief's "do not delete the failed release
# immediately."
st_rb_switch_release() {
    local rollback_target="$1" upgrade_id="$2" failed_version="$3" failure_step="$4"
    git -C "$SILVERTASK_SOURCE_DIR" checkout "v$rollback_target" >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1 || return 1

    local failed_dir="${SILVERTASK_PUBLISH_DIR}.failed"
    local previous_dir="${SILVERTASK_PUBLISH_DIR}.previous"
    rm -rf "$failed_dir"
    if [ -d "$SILVERTASK_PUBLISH_DIR" ]; then
        mv "$SILVERTASK_PUBLISH_DIR" "$failed_dir" || return 1
        cat > "${failed_dir}.json" <<EOF
{
  "version": "$(st_json_escape "$failed_version")",
  "upgradeId": "$(st_json_escape "$upgrade_id")",
  "failureStep": "$(st_json_escape "$failure_step")",
  "preservedAtUtc": "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
}
EOF
        chmod 640 "${failed_dir}.json" 2>/dev/null || true
    fi
    [ -d "$previous_dir" ] || return 1
    mv "$previous_dir" "$SILVERTASK_PUBLISH_DIR" || return 1
    chown -R "$SILVERTASK_SERVICE_USER:$SILVERTASK_SERVICE_USER" "$SILVERTASK_PUBLISH_DIR"
    return 0
}
