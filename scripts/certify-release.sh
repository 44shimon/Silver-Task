#!/bin/bash
# Silver Task — automated upgrade testing & release certification (Phase 57).
#
# Orchestrates a full install -> upgrade -> validate -> rollback -> validate lifecycle test
# against a target host, using the existing install-debian.sh / update-debian.sh /
# uninstall-debian.sh scripts exactly as an operator would run them by hand — never a second,
# parallel install/upgrade mechanism. The result is a durable, independently-verified
# certification report a maintainer or CI can check before tagging/publishing a release to the
# stable channel. See docs/release-certification.md for the full workflow.
#
# ****************************************************************************************
# * THIS SCRIPT INSTALLS, UPGRADES, ROLLS BACK, AND (with --cleanup) UNINSTALLS SILVER TASK *
# * ON WHATEVER HOST IT RUNS ON. IT MUST ONLY EVER BE RUN ON A DISPOSABLE, NON-PRODUCTION   *
# * HOST — A SPARE VM OR CONTAINER YOU ARE WILLING TO HAVE FULLY REBUILT. NEVER RUN THIS ON *
# * A HOST THAT ALREADY SERVES REAL USERS OR HOLDS REAL DATA.                              *
# ****************************************************************************************
#
# Usage:
#   sudo ./scripts/certify-release.sh --candidate=1.2.0 --disposable-host-confirmed
#   sudo ./scripts/certify-release.sh --candidate=1.2.0 --baseline=1.1.0 --channel=beta \
#       --disposable-host-confirmed --yes --cleanup
#   sudo ./scripts/certify-release.sh --help
#
# --candidate=X.Y.Z              (required) the release to certify.
# --baseline=X.Y.Z               (optional) the version to install first and upgrade FROM.
#                                 Defaults to the latest discovered stable release.
# --channel=stable|beta          (optional, default stable) passed through to update-debian.sh's
#                                 own --channel for the candidate prepare/activate steps.
# --disposable-host-confirmed    (required) explicit acknowledgment this host is expendable.
# --yes                          Skip the interactive typed confirmation (for CI). Still requires
#                                 --disposable-host-confirmed.
# --cleanup                      Run uninstall-debian.sh --remove-data --force at the end,
#                                 regardless of outcome. Default: leave the installation in place.
# --with-performance              (Phase 60) Also run scripts/test-performance.sh once against the
#                                 live baseline and once against the live candidate, and report any
#                                 regression between them. Never blocks certification by itself —
#                                 see --fail-on-regression. No dataset is seeded as part of this
#                                 (PerformanceDataSeeder is Development-only; this installs in
#                                 Production mode like a real deployment) — measurements run
#                                 against whatever data exists, which is usually little to none.
#                                 For a real, dataset-driven performance run, use
#                                 scripts/test-performance.sh directly against a dev/test instance.
# --fail-on-regression             With --with-performance: a detected regression blocks
#                                 certification (exit 10) instead of only being reported.
#
# Exit codes (this script's own independent scheme — not update-debian.sh's):
#   0  CERTIFIED — every required stage passed
#   1  general error
#   2  invalid arguments, or the disposable-host safety gate was not satisfied
#   3  baseline install failed
#   4  baseline health/version check failed
#   5  candidate prepare (validate/stage) failed
#   6  candidate activation failed
#   7  candidate validation (post-activation health/version) failed
#   8  rollback failed
#   9  rollback validation (baseline health/version not restored) failed
#   10 performance regression detected (only with --with-performance --fail-on-regression)
#
# A NOT_CERTIFIED report is still written on any failure (0 is the only "fully passed" exit).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"
# shellcheck source=lib/upgrade.sh
source "$SCRIPT_DIR/lib/upgrade.sh"
# shellcheck source=lib/certify.sh
source "$SCRIPT_DIR/lib/certify.sh"
# shellcheck source=lib/perf.sh
source "$SCRIPT_DIR/lib/perf.sh"

st_cert_usage() {
    cat <<'EOF'
Usage: sudo ./scripts/certify-release.sh --candidate=X.Y.Z [options]

THIS SCRIPT INSTALLS, UPGRADES, ROLLS BACK, AND (with --cleanup) UNINSTALLS SILVER TASK ON
WHATEVER HOST IT RUNS ON. Only ever run it on a disposable, non-production host.

  --candidate=X.Y.Z             Required. The release to certify.
  --baseline=X.Y.Z              Optional. Version to install first and upgrade FROM. Defaults to
                                 the latest discovered stable release.
  --channel=stable|beta         Optional, default stable. Passed through to update-debian.sh for
                                 the candidate prepare/activate steps.
  --disposable-host-confirmed   Required. Explicit acknowledgment this host is expendable.
  --yes                         Skip the interactive typed confirmation (for CI). Still requires
                                 --disposable-host-confirmed.
  --cleanup                     Run uninstall-debian.sh --remove-data --force at the end,
                                 regardless of outcome. Default: leave the installation in place.
  --with-performance             (Phase 60) Also measure performance against the live baseline and
                                 candidate and report any regression between them. Never blocks by
                                 itself — see --fail-on-regression.
  --fail-on-regression           With --with-performance: a detected regression blocks
                                 certification (exit 10) instead of only being reported.
  --help, -h                    Show this message.

See docs/release-certification.md for the full workflow and what each stage checks, and
docs/performance.md for what --with-performance measures and its limitations in this context.
EOF
}

CANDIDATE=""
BASELINE=""
CHANNEL="stable"
DISPOSABLE_CONFIRMED=false
ASSUME_YES=false
CLEANUP=false
WITH_PERFORMANCE=false
FAIL_ON_REGRESSION=false

while [ $# -gt 0 ]; do
    case "$1" in
        --candidate=*) CANDIDATE="${1#*=}" ;;
        --baseline=*) BASELINE="${1#*=}" ;;
        --channel=*)
            CHANNEL="${1#*=}"
            if [ "$CHANNEL" != "stable" ] && [ "$CHANNEL" != "beta" ]; then
                st_cert_usage >&2; echo "ERROR: --channel must be \"stable\" or \"beta\" (got \"$CHANNEL\")." >&2; exit 2
            fi
            ;;
        --disposable-host-confirmed) DISPOSABLE_CONFIRMED=true ;;
        --yes) ASSUME_YES=true ;;
        --cleanup) CLEANUP=true ;;
        --with-performance) WITH_PERFORMANCE=true ;;
        --fail-on-regression) FAIL_ON_REGRESSION=true ;;
        --help|-h) st_cert_usage; exit 0 ;;
        *)
            st_cert_usage >&2
            echo "ERROR: Unknown argument: $1" >&2
            exit 2
            ;;
    esac
    shift
done

if [ -z "$CANDIDATE" ]; then
    st_cert_usage >&2
    echo "ERROR: --candidate=X.Y.Z is required." >&2
    exit 2
fi
CANDIDATE_VALID=false
if st_up_semver_valid "$CANDIDATE"; then
    CANDIDATE_VALID=true
elif [ "$CHANNEL" = "beta" ] && st_up_semver_valid_prerelease "$CANDIDATE"; then
    CANDIDATE_VALID=true
fi
if [ "$CANDIDATE_VALID" != true ]; then
    st_cert_usage >&2
    if [ "$CHANNEL" = "beta" ]; then
        echo "ERROR: --candidate \"$CANDIDATE\" is not a valid version (expected MAJOR.MINOR.PATCH or, on the beta channel, MAJOR.MINOR.PATCH-identifier)." >&2
    else
        echo "ERROR: --candidate \"$CANDIDATE\" is not a valid version (expected MAJOR.MINOR.PATCH, e.g. 1.1.0). Pre-release versions require --channel=beta." >&2
    fi
    exit 2
fi
if [ -n "$BASELINE" ] && ! st_up_semver_valid "$BASELINE"; then
    st_cert_usage >&2
    echo "ERROR: --baseline \"$BASELINE\" is not a valid version (expected MAJOR.MINOR.PATCH, e.g. 1.1.0)." >&2
    exit 2
fi
if [ -n "$BASELINE" ] && [ "$BASELINE" = "$CANDIDATE" ]; then
    st_cert_usage >&2
    echo "ERROR: --baseline and --candidate must be different versions (nothing to upgrade)." >&2
    exit 2
fi
if [ "$FAIL_ON_REGRESSION" = true ] && [ "$WITH_PERFORMANCE" != true ]; then
    st_cert_usage >&2
    echo "ERROR: --fail-on-regression requires --with-performance." >&2
    exit 2
fi
if [ "$DISPOSABLE_CONFIRMED" != true ]; then
    st_cert_usage >&2
    echo "" >&2
    echo "ERROR: --disposable-host-confirmed is required." >&2
    echo "This script installs, upgrades, rolls back, and (with --cleanup) uninstalls Silver Task" >&2
    echo "on whatever host it runs on. Only pass this flag if you are certain this host is" >&2
    echo "disposable — a spare VM/container you are willing to have fully rebuilt, never a host" >&2
    echo "that already serves real users or holds real data." >&2
    exit 2
fi

st_require_root "$@"

if [ "$ASSUME_YES" != true ]; then
    echo ""
    echo "WARNING: this will install, upgrade, roll back, and validate Silver Task on this host."
    echo "It requires --disposable-host-confirmed, which you've passed, but this is your last"
    echo "chance to confirm interactively before anything happens."
    if ! st_confirm_destructive "About to certify candidate $CANDIDATE on this host." "$CANDIDATE"; then
        echo "Aborted by administrator (certification not confirmed)."
        exit 2
    fi
fi

if [ -z "$BASELINE" ]; then
    st_step "Resolving baseline (latest stable release)"
    if ! BASELINE="$(st_up_discover_stable_releases | tail -1)" || [ -z "$BASELINE" ]; then
        st_error "Could not discover a baseline release from $SILVERTASK_REPO_URL and none was given via --baseline."
        exit 1
    fi
    st_info "Baseline resolved to: $BASELINE"
fi

CERT_ID="$(st_cert_generate_id)"
REPORT_PATH="$(st_cert_report_path "$CANDIDATE" "$CERT_ID")"
st_cert_report_start "$REPORT_PATH" "$CERT_ID" "$CANDIDATE" "$BASELINE" "$CHANNEL"
st_cert_log "certify: starting (id=$CERT_ID candidate=$CANDIDATE baseline=$BASELINE channel=$CHANNEL cleanup=$CLEANUP)"

echo ""
echo "Silver Task Release Certification"
echo ""
echo "Certification ID: $CERT_ID"
echo "Baseline Version: $BASELINE"
echo "Candidate Version: $CANDIDATE"
echo "Channel: $CHANNEL"
echo "Report: $REPORT_PATH"
echo ""

# Runs a stage's command, records PASS/FAIL to the report, and — only on failure — writes the
# NOT_CERTIFIED verdict and exits immediately with that stage's designated code. A later stage
# never runs after an earlier one has failed (e.g. there is no point validating a rollback that
# never happened).
run_stage() {
    local name="$1" fail_exit_code="$2"; shift 2
    st_step "$name"
    st_cert_log "stage: $name starting"
    local rc=0
    "$@" >> "$SILVERTASK_CERTIFICATION_LOG_FILE" 2>&1 || rc=$?
    if [ "$rc" -eq 0 ]; then
        st_cert_report_stage "$REPORT_PATH" "$name" "PASS" "0" ""
        st_cert_log "stage: $name PASS"
        st_info "[OK] $name"
    else
        st_cert_report_stage "$REPORT_PATH" "$name" "FAIL" "$rc" "see $SILVERTASK_CERTIFICATION_LOG_FILE"
        st_cert_report_finish "$REPORT_PATH" "NOT_CERTIFIED" "$name"
        st_cert_log "stage: $name FAIL (exit $rc)"
        st_error "CERTIFICATION FAILED at stage: $name (exit $rc)"
        st_error "Report: $REPORT_PATH"
        st_error "Full stage output: $SILVERTASK_CERTIFICATION_LOG_FILE"
        exit "$fail_exit_code"
    fi
}

stage_baseline_install() {
    git -C "$REPO_ROOT" fetch --tags &&
    git -C "$REPO_ROOT" checkout "v$BASELINE" &&
    "$SCRIPT_DIR/install-debian.sh" --non-interactive --skip-ssl --skip-firewall
}

stage_baseline_health() {
    st_health_check "http://127.0.0.1:5000" 15 3 || return 1
    local running
    running="$(st_up_running_version "http://127.0.0.1:5000" || true)"
    [ "$running" = "$BASELINE" ]
}

stage_candidate_prepare() {
    "$SCRIPT_DIR/update-debian.sh" --target-version "$CANDIDATE" --channel="$CHANNEL" --yes
}

stage_candidate_activate() {
    "$SCRIPT_DIR/update-debian.sh" --activate --yes
}

stage_candidate_validate() {
    st_health_check "http://127.0.0.1:5000" 15 3 || return 1
    local running
    running="$(st_up_running_version "http://127.0.0.1:5000" || true)"
    [ "$running" = "$CANDIDATE" ]
}

stage_rollback() {
    "$SCRIPT_DIR/update-debian.sh" --rollback --yes
}

stage_rollback_validate() {
    st_health_check "http://127.0.0.1:5000" 15 3 || return 1
    local running
    running="$(st_up_running_version "http://127.0.0.1:5000" || true)"
    [ "$running" = "$BASELINE" ]
}

# --- Phase 60 — optional performance measurement (--with-performance), non-fatal by itself
# (a measurement failure is recorded but never calls exit — this is a diagnostic add-on, not part
# of the core install/upgrade/rollback lifecycle actually being certified). No dataset is seeded
# (PerformanceDataSeeder is Development-only; install-debian.sh runs the app in Production mode
# here, same as a real deployment) — measurements run against whatever data exists, usually little
# to none. Real, dataset-driven performance testing is scripts/test-performance.sh run directly.
st_cert_perf_extract() {
    local json="$1" key="$2"
    printf '%s' "$json" | sed -n "s/.*\"$key\":\([0-9][0-9]*\).*/\1/p"
}

run_performance_stage() {
    local label="$1"
    st_step "performance ($label)"
    local rc=0
    "$SCRIPT_DIR/test-performance.sh" --target-env=test --base-url=http://127.0.0.1:5000 \
        --dataset=small --profile=smoke --yes --json >> "$SILVERTASK_CERTIFICATION_LOG_FILE" 2>&1 || rc=$?
    local ops
    ops="$(st_perf_history_read 1 2>/dev/null | sed -n 's/.*"operations":\({[^}]*}\).*/\1/p' | head -1)"
    if [ "$rc" -eq 0 ]; then
        st_cert_report_stage "$REPORT_PATH" "performance_$label" "PASS" "0" "$ops"
        st_info "[OK] performance ($label)"
    else
        st_cert_report_stage "$REPORT_PATH" "performance_$label" "FAIL" "$rc" "see $SILVERTASK_CERTIFICATION_LOG_FILE"
        st_warn "Performance measurement failed for $label (exit $rc) — non-blocking, see $SILVERTASK_CERTIFICATION_LOG_FILE"
    fi
    printf '%s' "$ops"
}

run_stage "baseline_install"     3 stage_baseline_install
run_stage "baseline_health"      4 stage_baseline_health
PERF_BASELINE_OPS=""
PERF_CANDIDATE_OPS=""
if [ "$WITH_PERFORMANCE" = true ]; then
    PERF_BASELINE_OPS="$(run_performance_stage baseline)"
fi
run_stage "candidate_prepare"    5 stage_candidate_prepare
run_stage "candidate_activate"   6 stage_candidate_activate
run_stage "candidate_validate"   7 stage_candidate_validate
if [ "$WITH_PERFORMANCE" = true ]; then
    PERF_CANDIDATE_OPS="$(run_performance_stage candidate)"
fi
run_stage "rollback"             8 stage_rollback
run_stage "rollback_validate"    9 stage_rollback_validate

# --- Regression comparison — reported, never blocking unless --fail-on-regression. A regression
# is a >=50% slowdown with at least a 50ms absolute difference (so a 5ms->8ms blip on trivial data
# never reads as an alarming "60% regression"). ---
if [ "$WITH_PERFORMANCE" = true ] && [ -n "$PERF_BASELINE_OPS" ] && [ -n "$PERF_CANDIDATE_OPS" ]; then
    st_step "performance regression comparison"
    REGRESSION_FOUND=false
    for op in login dashboard my_tasks project_sheet filter search_common admin_users; do
        baseline_ms="$(st_cert_perf_extract "$PERF_BASELINE_OPS" "$op")"
        candidate_ms="$(st_cert_perf_extract "$PERF_CANDIDATE_OPS" "$op")"
        [ -n "$baseline_ms" ] && [ -n "$candidate_ms" ] || continue
        is_regression="$(awk -v b="$baseline_ms" -v c="$candidate_ms" 'BEGIN { print (c > b * 1.5 && c - b > 50) ? "yes" : "no" }')"
        if [ "$is_regression" = "yes" ]; then
            REGRESSION_FOUND=true
            st_warn "PERFORMANCE REGRESSION: $op ${baseline_ms}ms -> ${candidate_ms}ms"
        fi
    done
    if [ "$REGRESSION_FOUND" = true ]; then
        st_cert_report_stage "$REPORT_PATH" "performance_regression_check" "FAIL" "" "regression(s) detected — see $SILVERTASK_CERTIFICATION_LOG_FILE"
        if [ "$FAIL_ON_REGRESSION" = true ]; then
            st_cert_report_finish "$REPORT_PATH" "NOT_CERTIFIED" "performance_regression_check"
            st_cert_log "certify: FAILED, performance regression detected with --fail-on-regression"
            st_error "CERTIFICATION FAILED — performance regression detected and --fail-on-regression was passed."
            st_error "Report: $REPORT_PATH"
            exit 10
        fi
    else
        st_cert_report_stage "$REPORT_PATH" "performance_regression_check" "PASS" "0" "no regression detected"
        st_info "[OK] no performance regression detected"
    fi
fi

# Every required stage passed — the verdict is locked in as CERTIFIED here, before cleanup runs,
# so a teardown failure below can never flip a genuinely certified result to NOT_CERTIFIED (see
# docs/release-certification.md — cleanup is reported but never affects the verdict).
st_cert_report_finish "$REPORT_PATH" "CERTIFIED" ""
st_cert_log "certify: CERTIFIED (id=$CERT_ID candidate=$CANDIDATE baseline=$BASELINE)"

CLEANUP_STATUS="SKIPPED"
if [ "$CLEANUP" = true ]; then
    st_step "cleanup"
    if "$SCRIPT_DIR/uninstall-debian.sh" --remove-data --force >> "$SILVERTASK_CERTIFICATION_LOG_FILE" 2>&1; then
        CLEANUP_STATUS="PASS"
        st_info "[OK] cleanup"
    else
        CLEANUP_STATUS="FAIL"
        st_warn "Cleanup (uninstall --remove-data --force) failed — the host was left installed."
        st_warn "This does NOT affect the CERTIFIED verdict above; see $SILVERTASK_CERTIFICATION_LOG_FILE."
    fi
    st_cert_report_stage "$REPORT_PATH" "cleanup" "$CLEANUP_STATUS" "" ""
fi

echo ""
st_info "=================================================================="
st_info " CERTIFIED"
st_info " $BASELINE -> $CANDIDATE (channel: $CHANNEL)"
st_info " Certification ID: $CERT_ID"
st_info " Report: $REPORT_PATH"
st_info "=================================================================="
exit 0
