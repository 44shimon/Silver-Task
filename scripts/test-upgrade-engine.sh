#!/bin/bash
# Silver Task — standalone tests for scripts/lib/upgrade.sh's portable logic (Phase 52).
#
# Deliberately root/Debian/flock-independent: exercises semver validation/comparison/sorting,
# stable-tag discovery/filtering, version detection against on-disk fixtures, and release metadata
# validation — everything that's pure string/file processing. flock-based locking, git-worktree
# staging, and systemd/service interaction are reviewed and bash -n syntax-checked separately (see
# the Phase 52 final report) but need a real Debian host to exercise end-to-end, the same
# limitation every Debian-specific script in this repo has always had outside that environment.
#
# Usage: bash scripts/test-upgrade-engine.sh

# Deliberately no `-e`, unlike every other script in this repo (lib/common.sh's own doc comment
# says every sourcing script must set `-euo pipefail`) — several assertions below call functions
# expected to return non-zero directly (e.g. `st_up_metadata_validate ...; RC=$?`), and `-e` would
# abort the whole test run on the first one, before RC is even captured.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_ROOT="$(mktemp -d)"
trap 'rm -rf "$TEST_ROOT"' EXIT

# Point every SILVERTASK_* location at the throwaway test root before sourcing common.sh — its
# `: "${VAR:=default}"` guards mean these pre-set values win, so nothing this test does can ever
# touch a real installation, /opt, or /var/log.
export SILVERTASK_INSTALL_DIR="$TEST_ROOT/install"
export SILVERTASK_LOG_FILE="$TEST_ROOT/install.log"
export SILVERTASK_UPGRADE_LOCK_FILE="$TEST_ROOT/upgrade.lock"
export SILVERTASK_UPGRADE_STATE_FILE="$TEST_ROOT/install/upgrade-state.json"
export SILVERTASK_UPGRADE_STAGING_DIR="$TEST_ROOT/install/upgrade-staging"
export SILVERTASK_UPGRADE_LOG_DIR="$TEST_ROOT/upgrade-log"
export SILVERTASK_UPGRADE_LOG_FILE="$TEST_ROOT/upgrade-log/upgrade.log"
export SILVERTASK_BACKUP_DIR="$TEST_ROOT/backups"
export SILVERTASK_MAINTENANCE_FLAG_FILE="$TEST_ROOT/install/maintenance.json"
mkdir -p "$SILVERTASK_INSTALL_DIR" "$SILVERTASK_UPGRADE_LOG_DIR" "$SILVERTASK_BACKUP_DIR"

# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"
# shellcheck source=lib/upgrade.sh
source "$SCRIPT_DIR/lib/upgrade.sh"

PASS=0
FAIL=0

assert_true() {
    local desc="$1"; shift
    if "$@" >/dev/null 2>&1; then
        PASS=$((PASS + 1))
    else
        FAIL=$((FAIL + 1)); echo "FAIL: $desc"
    fi
}

assert_false() {
    local desc="$1"; shift
    if ! "$@" >/dev/null 2>&1; then
        PASS=$((PASS + 1))
    else
        FAIL=$((FAIL + 1)); echo "FAIL: $desc (expected failure, got success)"
    fi
}

assert_eq() {
    local desc="$1" expected="$2" actual="$3"
    if [ "$expected" = "$actual" ]; then
        PASS=$((PASS + 1))
    else
        FAIL=$((FAIL + 1)); echo "FAIL: $desc (expected [$expected], got [$actual])"
    fi
}

echo "== st_up_semver_valid =="
assert_true  "1.0.0 is valid"              st_up_semver_valid "1.0.0"
assert_true  "1.10.0 is valid"             st_up_semver_valid "1.10.0"
assert_true  "20.3.100 is valid"           st_up_semver_valid "20.3.100"
assert_false "1.0 is invalid (2 parts)"    st_up_semver_valid "1.0"
assert_false "v1.0.0 is invalid (has v)"   st_up_semver_valid "v1.0.0"
assert_false "1.0.0-beta is invalid"       st_up_semver_valid "1.0.0-beta"
assert_false "1.0.0-rc1 is invalid"        st_up_semver_valid "1.0.0-rc1"
assert_false "empty is invalid"            st_up_semver_valid ""
assert_false "shell-injection attempt"     st_up_semver_valid "1.0.1; rm -rf /"
assert_false "latest is invalid"           st_up_semver_valid "latest"
assert_false "main is invalid"             st_up_semver_valid "main"

echo "== st_up_semver_compare =="
assert_eq "1.0.0 == 1.0.0" "0" "$(st_up_semver_compare 1.0.0 1.0.0)"
assert_eq "1.0.0 < 1.0.1" "-1" "$(st_up_semver_compare 1.0.0 1.0.1)"
assert_eq "1.0.1 > 1.0.0" "1" "$(st_up_semver_compare 1.0.1 1.0.0)"
assert_eq "1.1.0 > 1.0.9" "1" "$(st_up_semver_compare 1.1.0 1.0.9)"
assert_eq "1.9.0 < 1.10.0 (numeric, not lexicographic)" "-1" "$(st_up_semver_compare 1.9.0 1.10.0)"
assert_eq "1.10.0 < 2.0.0" "-1" "$(st_up_semver_compare 1.10.0 2.0.0)"
assert_eq "2.0.0 > 1.99.99" "1" "$(st_up_semver_compare 2.0.0 1.99.99)"

echo "== st_up_semver_sort =="
SORTED="$(printf '1.9.0\n1.10.0\n2.0.0\n1.0.1\n1.0.0\n' | st_up_semver_sort)"
assert_eq "sort orders 1.9.0 before 1.10.0 before 2.0.0" \
    "$(printf '1.0.0\n1.0.1\n1.9.0\n1.10.0\n2.0.0')" "$SORTED"

echo "== st_up_filter_stable_tags (simulated git ls-remote --tags output) =="
RAW_TAGS='19efe12c86148a2125e3e6c32babe98e27b450ac	refs/tags/v1.0.0
87c49771d2f1bbc3d67193b3f1dc3bc591b83438	refs/tags/v1.0.1
aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa	refs/tags/v1.1.0-beta
bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb	refs/tags/v1.1.0-rc1
cccccccccccccccccccccccccccccccccccccccc	refs/tags/v1.9.0
dddddddddddddddddddddddddddddddddddddddd	refs/tags/v1.10.0
eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee	refs/tags/v2.0.0
ffffffffffffffffffffffffffffffffffffffff	refs/tags/v2.0.0^{}
0000000000000000000000000000000000000000	refs/tags/latest
1111111111111111111111111111111111111111	refs/tags/main
2222222222222222222222222222222222222222	refs/tags/development
3333333333333333333333333333333333333333	refs/heads/main
4444444444444444444444444444444444444444	refs/tags/vX.Y.Z'
FILTERED="$(printf '%s\n' "$RAW_TAGS" | st_up_filter_stable_tags)"
assert_eq "pre-release/peeled/branch-like/malformed tags excluded; numeric sort" \
    "$(printf '1.0.0\n1.0.1\n1.9.0\n1.10.0\n2.0.0')" "$FILTERED"
assert_eq "latest of filtered set is 2.0.0" "2.0.0" "$(printf '%s\n' "$FILTERED" | tail -1)"

echo "== st_up_installed_version / st_up_installed_commit (on-disk fixture) =="
cat > "$SILVERTASK_INSTALL_DIR/installed-version.json" <<'EOF'
{
  "version": "1.0.1",
  "gitCommit": "abc1234",
  "installedAtUtc": "2026-01-01T00:00:00Z"
}
EOF
assert_eq "installed version read from fixture" "1.0.1" "$(st_up_installed_version)"
assert_eq "installed commit read from fixture" "abc1234" "$(st_up_installed_commit)"

echo "== st_up_running_version (unreachable app) =="
assert_false "unreachable app returns failure, not a false version" \
    st_up_running_version "http://127.0.0.1:1"

echo "== st_up_version_consistency =="
st_up_version_consistency
assert_eq "unreachable running app => UNKNOWN, not MISMATCH" "UNKNOWN" "$ST_UP_CONSISTENCY"

echo "== st_up_metadata_validate =="
VALID_META='{
  "version": "1.1.0",
  "channel": "stable",
  "minimumSupportedVersion": "1.0.0",
  "requiresDatabaseMigration": true,
  "requiresDataMigration": false,
  "requiresRestart": true
}'
st_up_metadata_validate "1.1.0" "$VALID_META" "1.0.1"; RC=$?
assert_eq "valid metadata returns 0" "0" "$RC"
assert_eq "requiresDatabaseMigration parsed as true" "true" "$ST_UP_META_REQUIRES_DB_MIGRATION"
assert_eq "requiresRestart parsed as true" "true" "$ST_UP_META_REQUIRES_RESTART"

st_up_metadata_validate "1.1.0" "" "1.0.1"; RC=$?
assert_eq "missing metadata (empty content) uses safe defaults, returns 0" "0" "$RC"
assert_eq "default requiresDatabaseMigration is conservative (true)" "true" "$ST_UP_META_REQUIRES_DB_MIGRATION"
assert_eq "default requiresRestart is true" "true" "$ST_UP_META_REQUIRES_RESTART"

WRONG_VERSION_META='{"version": "1.2.0", "channel": "stable"}'
st_up_metadata_validate "1.1.0" "$WRONG_VERSION_META" "1.0.1"; RC=$?
assert_eq "version-field mismatch is malformed (1)" "1" "$RC"

BAD_CHANNEL_META='{"version": "1.1.0", "channel": "beta"}'
st_up_metadata_validate "1.1.0" "$BAD_CHANNEL_META" "1.0.1"; RC=$?
assert_eq "unsupported channel is malformed (1)" "1" "$RC"

BAD_MIN_VERSION_META='{"version": "1.1.0", "channel": "stable", "minimumSupportedVersion": "not-a-version"}'
st_up_metadata_validate "1.1.0" "$BAD_MIN_VERSION_META" "1.0.1"; RC=$?
assert_eq "invalid minimumSupportedVersion is malformed (1)" "1" "$RC"

MIN_VERSION_NOT_MET_META='{"version": "2.0.0", "channel": "stable", "minimumSupportedVersion": "1.1.0"}'
st_up_metadata_validate "2.0.0" "$MIN_VERSION_NOT_MET_META" "1.0.1"; RC=$?
assert_eq "installed below minimumSupportedVersion blocks upgrade (2)" "2" "$RC"

MIN_VERSION_MET_META='{"version": "2.0.0", "channel": "stable", "minimumSupportedVersion": "1.0.0"}'
st_up_metadata_validate "2.0.0" "$MIN_VERSION_MET_META" "1.0.1"; RC=$?
assert_eq "installed at/above minimumSupportedVersion allows upgrade (0)" "0" "$RC"

echo "== st_up_state_write / st_up_state_read round-trip =="
st_up_state_write "PREPARING" "1.0.1" "1.1.0" "staging release" "2026-01-01T00:00:00Z"
STATE_OUTPUT="$(st_up_state_read)"
assert_true "state file readable after write" test -f "$SILVERTASK_UPGRADE_STATE_FILE"
echo "$STATE_OUTPUT" | grep -q '^status=PREPARING$' && PASS=$((PASS + 1)) || { FAIL=$((FAIL + 1)); echo "FAIL: state status round-trips as PREPARING"; }
echo "$STATE_OUTPUT" | grep -q '^targetVersion=1.1.0$' && PASS=$((PASS + 1)) || { FAIL=$((FAIL + 1)); echo "FAIL: state targetVersion round-trips as 1.1.0"; }

echo "== st_up_disk_space_check (smoke test — just must not crash and must set outputs) =="
st_up_disk_space_check || true
assert_true "ST_UP_DISK_REQUIRED_MB was set" test -n "${ST_UP_DISK_REQUIRED_MB:-}"

echo "== st_up_generate_upgrade_id (Phase 53) =="
UPGRADE_ID="$(st_up_generate_upgrade_id)"
if [[ "$UPGRADE_ID" =~ ^upgrade-[0-9]{8}-[0-9]{6}-[0-9a-f]{6}$ ]]; then
    PASS=$((PASS + 1))
else
    FAIL=$((FAIL + 1)); echo "FAIL: upgrade ID \"$UPGRADE_ID\" does not match expected format"
fi

echo "== st_up_path_is_under (Phase 53) =="
assert_true  "identical paths are \"under\""          st_up_path_is_under "$SILVERTASK_SOURCE_DIR" "$SILVERTASK_SOURCE_DIR"
assert_true  "nested path is under its parent"        st_up_path_is_under "$SILVERTASK_SOURCE_DIR/App_Data/attachments" "$SILVERTASK_SOURCE_DIR"
assert_false "sibling with shared prefix is not under" st_up_path_is_under "${SILVERTASK_SOURCE_DIR}-other" "$SILVERTASK_SOURCE_DIR"
assert_false "unrelated external path is not under"    st_up_path_is_under "/var/lib/silver-task/attachments" "$SILVERTASK_SOURCE_DIR"

echo "== st_up_persistent_data_check (Phase 53) — the brief's own 'uploads inside the build directory' case =="
UNSAFE_ENV="$TEST_ROOT/unsafe.env"
echo "Attachments__StorageRoot=$SILVERTASK_SOURCE_DIR/App_Data/attachments" > "$UNSAFE_ENV"
if st_up_persistent_data_check "$UNSAFE_ENV"; then
    FAIL=$((FAIL + 1)); echo "FAIL: storage root inside SOURCE_DIR should have been flagged unsafe"
else
    PASS=$((PASS + 1))
    [ -n "$ST_UP_PERSISTENT_DATA_ISSUE" ] && PASS=$((PASS + 1)) || { FAIL=$((FAIL + 1)); echo "FAIL: expected a populated issue message"; }
fi

SAFE_ENV="$TEST_ROOT/safe.env"
echo "Attachments__StorageRoot=/var/lib/silver-task/attachments" > "$SAFE_ENV"
assert_true "storage root outside install tree is safe" st_up_persistent_data_check "$SAFE_ENV"

echo "== st_up_backup_manifest_status (Phase 53, fixture manifests — no pg_dump/DB needed) =="
FIXTURE_DIR="$TEST_ROOT/backup-fixture"
mkdir -p "$FIXTURE_DIR"

cat > "$FIXTURE_DIR/manifest.json" <<'EOF'
{"type":"pre-upgrade","databaseBackup":"","databaseBackupVerified":false,"configurationBackup":"","configurationBackupVerified":false}
EOF
st_up_backup_manifest_status "$FIXTURE_DIR/manifest.json"; RC=$?
assert_eq "no database path yet => DATABASE_FAILED" "DATABASE_FAILED" "$ST_UP_BACKUP_RESULT"
assert_eq "returns non-zero when not OK" "1" "$RC"

cat > "$FIXTURE_DIR/manifest.json" <<'EOF'
{"type":"pre-upgrade","databaseBackup":"database.dump","databaseBackupVerified":false,"configurationBackup":"","configurationBackupVerified":false}
EOF
st_up_backup_manifest_status "$FIXTURE_DIR/manifest.json" || true
assert_eq "database created but not verified => DATABASE_UNVERIFIED" "DATABASE_UNVERIFIED" "$ST_UP_BACKUP_RESULT"

cat > "$FIXTURE_DIR/manifest.json" <<'EOF'
{"type":"pre-upgrade","databaseBackup":"database.dump","databaseBackupVerified":true,"configurationBackup":"","configurationBackupVerified":false}
EOF
st_up_backup_manifest_status "$FIXTURE_DIR/manifest.json" || true
assert_eq "database verified, no config path yet => CONFIG_FAILED" "CONFIG_FAILED" "$ST_UP_BACKUP_RESULT"

cat > "$FIXTURE_DIR/manifest.json" <<'EOF'
{"type":"pre-upgrade","databaseBackup":"database.dump","databaseBackupVerified":true,"configurationBackup":"silvertask.env","configurationBackupVerified":false}
EOF
st_up_backup_manifest_status "$FIXTURE_DIR/manifest.json" || true
assert_eq "config created but not verified => CONFIG_UNVERIFIED" "CONFIG_UNVERIFIED" "$ST_UP_BACKUP_RESULT"

cat > "$FIXTURE_DIR/manifest.json" <<'EOF'
{"type":"pre-upgrade","databaseBackup":"database.dump","databaseBackupVerified":true,"configurationBackup":"silvertask.env","configurationBackupVerified":true}
EOF
st_up_backup_manifest_status "$FIXTURE_DIR/manifest.json"; RC=$?
assert_eq "everything verified => OK" "OK" "$ST_UP_BACKUP_RESULT"
assert_eq "returns zero when OK" "0" "$RC"

assert_false "missing manifest file => UNKNOWN, non-zero" st_up_backup_manifest_status "$TEST_ROOT/does-not-exist.json"

echo "== st_up_migration_plan (Phase 53, fixture applied/target lists) =="
APPLIED_LIST="$(printf '20260101000000_A\n20260102000000_B\n')"
TARGET_LIST="$(printf '20260101000000_A\n20260102000000_B\n20260103000000_C\n')"
st_up_migration_plan "$APPLIED_LIST" "$TARGET_LIST" "false"
assert_eq "one pending migration detected" "1" "$ST_UP_MIGRATION_PENDING_COUNT"
assert_eq "pending migration is the new one" "20260103000000_C" "$ST_UP_MIGRATION_PENDING"
assert_eq "pending + no data migration => REQUIRES_BACKUP" "REQUIRES_BACKUP" "$ST_UP_MIGRATION_CLASSIFICATION"

st_up_migration_plan "$APPLIED_LIST" "$APPLIED_LIST" "false"
assert_eq "nothing pending" "0" "$ST_UP_MIGRATION_PENDING_COUNT"
assert_eq "no pending + no data migration => SAFE" "SAFE" "$ST_UP_MIGRATION_CLASSIFICATION"

st_up_migration_plan "$APPLIED_LIST" "$APPLIED_LIST" "true"
assert_eq "requiresDataMigration forces REQUIRES_MAINTENANCE_MODE even with nothing pending" \
    "REQUIRES_MAINTENANCE_MODE" "$ST_UP_MIGRATION_CLASSIFICATION"

echo "== st_is_backup_set_name / st_assert_safe_backup_dir (Phase 53, lib/common.sh) =="
assert_true  "well-formed timestamp name accepted" st_is_backup_set_name "20260901-120000"
assert_false "arbitrary name rejected"             st_is_backup_set_name "not-a-backup"
assert_false "wrong-format date rejected"          st_is_backup_set_name "2026-09-01"

test_unsafe_backup_dir() { ( st_assert_safe_backup_dir "$1" ) >/dev/null 2>&1; }
assert_false "refuses a relative path"     test_unsafe_backup_dir "relative/path"
assert_false "refuses a system directory"  test_unsafe_backup_dir "/etc"
assert_false "refuses a nonexistent path"  test_unsafe_backup_dir "$TEST_ROOT/does-not-exist"
assert_true  "accepts the real backup dir" st_assert_safe_backup_dir "$SILVERTASK_BACKUP_DIR"

echo "== st_up_maintenance_enable / st_up_maintenance_disable / st_up_maintenance_probe (Phase 54) =="
st_up_maintenance_probe
assert_eq "no flag file => not active" "false" "$ST_UP_MAINTENANCE_ACTIVE"

st_up_maintenance_enable "upgrade-test-1" "1.1.0"
st_up_maintenance_probe
assert_eq "enabled => active" "true" "$ST_UP_MAINTENANCE_ACTIVE"
assert_eq "upgrade id round-trips" "upgrade-test-1" "$ST_UP_MAINTENANCE_UPGRADE_ID"
assert_eq "target round-trips" "1.1.0" "$ST_UP_MAINTENANCE_TARGET"
assert_true "startedAtUtc populated" test -n "$ST_UP_MAINTENANCE_STARTED"

st_up_maintenance_disable
st_up_maintenance_probe
assert_eq "disabled => not active" "false" "$ST_UP_MAINTENANCE_ACTIVE"

echo "== st_up_activation_prerequisites_check (Phase 54) =="

rm -f "$SILVERTASK_UPGRADE_STATE_FILE"
assert_false "no prepared upgrade at all blocks activation" st_up_activation_prerequisites_check

# Writes a fully-OK READY_FOR_ACTIVATION state via the real st_up_state_write (not hand-rolled
# JSON), with an actually-existing staged worktree dir and backup dir+manifest on disk — tests
# below mutate exactly one thing away from "fully OK" per case.
write_ready_state() {
    local staged_dir="$SILVERTASK_UPGRADE_STAGING_DIR/1.1.0"
    local backup_dir="$TEST_ROOT/backups/fixture-ready"
    mkdir -p "$staged_dir" "$backup_dir"
    echo '{"type":"pre-upgrade"}' > "$backup_dir/manifest.json"
    ST_UP_STATE_BACKUP_STATUS="OK"
    ST_UP_STATE_BACKUP_VERIFICATION_STATUS="OK"
    ST_UP_STATE_PERSISTENT_DATA_STATUS="OK"
    ST_UP_STATE_MIGRATION_VALIDATION_STATUS="OK"
    ST_UP_STATE_MIGRATION_PLAN_STATUS="OK"
    ST_UP_STATE_MIGRATION_REQUIRED="true"
    ST_UP_STATE_BACKUP_DIR="$backup_dir"
    st_up_state_write "READY_FOR_ACTIVATION" "1.0.1" "1.1.0" "ready for activation" "2026-01-01T00:00:00Z" "upgrade-fixture"
}

write_ready_state
assert_true "fully-OK READY_FOR_ACTIVATION state passes prerequisites" st_up_activation_prerequisites_check
st_up_activation_prerequisites_check
assert_eq "populates target version" "1.1.0" "$ST_UP_ACTIVATE_TARGET_VERSION"
assert_eq "populates upgrade id" "upgrade-fixture" "$ST_UP_ACTIVATE_UPGRADE_ID"
assert_eq "populates current (previous) version" "1.0.1" "$ST_UP_ACTIVATE_CURRENT_VERSION"
assert_eq "populates backup dir" "$TEST_ROOT/backups/fixture-ready" "$ST_UP_ACTIVATE_BACKUP_DIR"
assert_eq "populates migration required" "true" "$ST_UP_ACTIVATE_MIGRATION_REQUIRED"

write_ready_state
st_up_state_write "FAILED" "1.0.1" "1.1.0" "some step" "2026-01-01T00:00:00Z" "upgrade-fixture"
assert_false "FAILED status blocks activation" st_up_activation_prerequisites_check

write_ready_state
ST_UP_STATE_BACKUP_VERIFICATION_STATUS="FAILED"
st_up_state_write "READY_FOR_ACTIVATION" "1.0.1" "1.1.0" "ready for activation" "2026-01-01T00:00:00Z" "upgrade-fixture"
assert_false "backupVerificationStatus != OK blocks activation" st_up_activation_prerequisites_check
st_up_activation_prerequisites_check
[ "$ST_UP_ACTIVATION_BLOCKED_REASON" != "" ] && PASS=$((PASS + 1)) || { FAIL=$((FAIL + 1)); echo "FAIL: blocked reason should be populated"; }

write_ready_state
rm -rf "$SILVERTASK_UPGRADE_STAGING_DIR/1.1.0"
assert_false "missing staged worktree blocks activation" st_up_activation_prerequisites_check

write_ready_state
rm -f "$TEST_ROOT/backups/fixture-ready/manifest.json"
assert_false "missing backup manifest blocks activation" st_up_activation_prerequisites_check

echo ""
echo "=================================================="
echo "Passed: $PASS  Failed: $FAIL"
echo "=================================================="
[ "$FAIL" -eq 0 ]
