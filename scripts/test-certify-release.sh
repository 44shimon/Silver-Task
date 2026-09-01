#!/bin/bash
# Silver Task — standalone tests for scripts/lib/certify.sh's portable logic and
# scripts/certify-release.sh's argument parsing/safety gate (Phase 57).
#
# Same deliberate scope as test-upgrade-engine.sh: pure string/file processing and argument
# validation, root/Debian/apt/systemd/PostgreSQL-independent. The actual install -> upgrade ->
# rollback lifecycle certify-release.sh orchestrates needs a real disposable Debian host to
# exercise end-to-end — reviewed and bash -n syntax-checked separately (see the Phase 57 final
# report), the same limitation every Debian-specific script in this repo has always had outside
# that environment.
#
# Usage: bash scripts/test-certify-release.sh

# Deliberately no `-e`, matching test-upgrade-engine.sh's own reasoning — several assertions below
# call functions/scripts expected to return non-zero directly, and `-e` would abort the whole run
# on the first one before the result is even captured.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_ROOT="$(mktemp -d)"
trap 'rm -rf "$TEST_ROOT"' EXIT

# Point every SILVERTASK_* location at the throwaway test root before sourcing common.sh, same
# guard-respecting approach test-upgrade-engine.sh already established.
export SILVERTASK_INSTALL_DIR="$TEST_ROOT/install"
export SILVERTASK_LOG_FILE="$TEST_ROOT/install.log"
export SILVERTASK_UPGRADE_LOG_DIR="$TEST_ROOT/upgrade-log"
export SILVERTASK_UPGRADE_LOG_FILE="$TEST_ROOT/upgrade-log/upgrade.log"
export SILVERTASK_CERTIFICATION_DIR="$TEST_ROOT/upgrade-log/certifications"
export SILVERTASK_CERTIFICATION_LOG_FILE="$TEST_ROOT/upgrade-log/certification.log"
mkdir -p "$SILVERTASK_INSTALL_DIR" "$SILVERTASK_UPGRADE_LOG_DIR" "$SILVERTASK_CERTIFICATION_DIR"

# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"
# shellcheck source=lib/certify.sh
source "$SCRIPT_DIR/lib/certify.sh"

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

echo "== st_cert_generate_id =="
CERT_ID="$(st_cert_generate_id)"
if [[ "$CERT_ID" =~ ^certify-[0-9]{8}-[0-9]{6}-[0-9a-f]{6}$ ]]; then
    PASS=$((PASS + 1))
else
    FAIL=$((FAIL + 1)); echo "FAIL: certification ID \"$CERT_ID\" does not match expected format"
fi

echo "== st_cert_report_path =="
assert_eq "report path is deterministic from candidate + id" \
    "$SILVERTASK_CERTIFICATION_DIR/certification-1.2.0-certify-abc.jsonl" \
    "$(st_cert_report_path "1.2.0" "certify-abc")"

echo "== st_cert_report_start / stage / finish round-trip (all-pass) =="
REPORT="$(st_cert_report_path "1.2.0" "certify-fixture-pass")"
st_cert_report_start "$REPORT" "certify-fixture-pass" "1.2.0" "1.1.0" "stable"
assert_true "report file created after start" test -f "$REPORT"
st_cert_report_stage "$REPORT" "baseline_install" "PASS" "0" ""
st_cert_report_stage "$REPORT" "baseline_health" "PASS" "0" ""
st_cert_report_stage "$REPORT" "candidate_prepare" "PASS" "0" ""
st_cert_report_stage "$REPORT" "candidate_activate" "PASS" "0" ""
st_cert_report_stage "$REPORT" "candidate_validate" "PASS" "0" ""
st_cert_report_stage "$REPORT" "rollback" "PASS" "0" ""
st_cert_report_stage "$REPORT" "rollback_validate" "PASS" "0" ""
assert_true "all-PASS stages compute a CERTIFIED verdict" st_cert_compute_verdict "$REPORT"
st_cert_compute_verdict "$REPORT"
assert_eq "verdict is CERTIFIED" "CERTIFIED" "$ST_CERT_VERDICT"
assert_eq "no failed stage recorded" "" "$ST_CERT_FAILED_STAGE"

echo "== SKIPPED cleanup stage never blocks certification =="
REPORT_SKIP="$(st_cert_report_path "1.2.0" "certify-fixture-skip")"
st_cert_report_start "$REPORT_SKIP" "certify-fixture-skip" "1.2.0" "1.1.0" "stable"
st_cert_report_stage "$REPORT_SKIP" "baseline_install" "PASS" "0" ""
st_cert_report_stage "$REPORT_SKIP" "rollback_validate" "PASS" "0" ""
st_cert_report_stage "$REPORT_SKIP" "cleanup" "SKIPPED" "" "not requested"
assert_true "SKIPPED stage doesn't block a CERTIFIED verdict" st_cert_compute_verdict "$REPORT_SKIP"

echo "== st_cert_report_stage / compute_verdict (a failing stage) =="
REPORT_FAIL="$(st_cert_report_path "1.2.0" "certify-fixture-fail")"
st_cert_report_start "$REPORT_FAIL" "certify-fixture-fail" "1.2.0" "1.1.0" "stable"
st_cert_report_stage "$REPORT_FAIL" "baseline_install" "PASS" "0" ""
st_cert_report_stage "$REPORT_FAIL" "baseline_health" "PASS" "0" ""
st_cert_report_stage "$REPORT_FAIL" "candidate_prepare" "FAIL" "5" "target version unavailable"
assert_false "a FAIL stage computes a NOT_CERTIFIED verdict" st_cert_compute_verdict "$REPORT_FAIL"
st_cert_compute_verdict "$REPORT_FAIL" || true
assert_eq "verdict is NOT_CERTIFIED" "NOT_CERTIFIED" "$ST_CERT_VERDICT"
assert_eq "failed stage is the one that actually failed" "candidate_prepare" "$ST_CERT_FAILED_STAGE"

echo "== compute_verdict reports only the FIRST failing stage =="
REPORT_MULTI="$(st_cert_report_path "1.2.0" "certify-fixture-multi")"
st_cert_report_start "$REPORT_MULTI" "certify-fixture-multi" "1.2.0" "1.1.0" "stable"
st_cert_report_stage "$REPORT_MULTI" "baseline_install" "FAIL" "3" ""
st_cert_report_stage "$REPORT_MULTI" "baseline_health" "FAIL" "4" ""
st_cert_compute_verdict "$REPORT_MULTI" || true
assert_eq "first failure wins, not the last" "baseline_install" "$ST_CERT_FAILED_STAGE"

echo "== st_cert_compute_verdict against a missing report =="
assert_false "missing report is NOT_CERTIFIED, non-zero" st_cert_compute_verdict "$TEST_ROOT/does-not-exist.jsonl"
st_cert_compute_verdict "$TEST_ROOT/does-not-exist.jsonl" 2>/dev/null || true
assert_eq "missing report reports a populated failedStage" "report missing" "$ST_CERT_FAILED_STAGE"

echo "== st_cert_report_read_stages filters to stage lines only =="
STAGE_LINES="$(st_cert_report_read_stages "$REPORT")"
assert_eq "7 stage lines read back for the all-pass fixture" "7" "$(printf '%s\n' "$STAGE_LINES" | grep -c '.')"
assert_eq "run-metadata line excluded" "0" "$(printf '%s\n' "$STAGE_LINES" | grep -c '"type":"run"')"

echo "== st_cert_log writes to the certification log file =="
st_cert_log "test log line" >/dev/null
assert_true "certification log file created" test -f "$SILVERTASK_CERTIFICATION_LOG_FILE"
grep -q "test log line" "$SILVERTASK_CERTIFICATION_LOG_FILE" && PASS=$((PASS + 1)) || { FAIL=$((FAIL + 1)); echo "FAIL: log line not found in certification log"; }

echo "== certify-release.sh argument parsing / safety gate (no root required for these paths) =="
CERTIFY="$SCRIPT_DIR/certify-release.sh"

run_certify() { bash "$CERTIFY" "$@" >/dev/null 2>&1; }

assert_eq "--help exits 0" "0" "$(bash "$CERTIFY" --help >/dev/null 2>&1; echo $?)"
assert_eq "missing --candidate exits 2" "2" "$(run_certify --disposable-host-confirmed; echo $?)"
assert_eq "invalid --candidate format exits 2" "2" "$(run_certify --candidate=abc --disposable-host-confirmed; echo $?)"
assert_eq "missing --disposable-host-confirmed exits 2" "2" "$(run_certify --candidate=1.2.0; echo $?)"
assert_eq "invalid --channel exits 2" "2" "$(run_certify --candidate=1.2.0 --channel=nightly --disposable-host-confirmed; echo $?)"
assert_eq "--baseline == --candidate exits 2" "2" "$(run_certify --candidate=1.2.0 --baseline=1.2.0 --disposable-host-confirmed; echo $?)"
assert_eq "invalid --baseline format exits 2" "2" "$(run_certify --candidate=1.2.0 --baseline=abc --disposable-host-confirmed; echo $?)"
assert_eq "prerelease --candidate on stable channel exits 2" "2" "$(run_certify --candidate=1.2.0-beta --disposable-host-confirmed; echo $?)"
assert_eq "unknown argument exits 2" "2" "$(run_certify --bogus; echo $?)"
# Everything below this point is fully valid — the only reason it can't proceed further in this
# sandbox is the root check, which is expected and confirms parsing/the safety gate passed cleanly.
assert_eq "valid args + beta channel + prerelease candidate reach the root check (exit 1, not 2)" \
    "1" "$(run_certify --candidate=1.2.0-beta --channel=beta --disposable-host-confirmed --yes; echo $?)"
assert_eq "fully valid stable-channel args reach the root check (exit 1, not 2)" \
    "1" "$(run_certify --candidate=1.2.0 --disposable-host-confirmed --yes; echo $?)"

echo ""
echo "=================================================="
echo "Passed: $PASS  Failed: $FAIL"
echo "=================================================="
[ "$FAIL" -eq 0 ]
