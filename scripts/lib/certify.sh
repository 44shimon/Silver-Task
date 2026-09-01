#!/bin/bash
# Silver Task — automated upgrade testing & release certification (Phase 57).
#
# Portable bookkeeping/reporting logic for scripts/certify-release.sh, split out the same way
# lib/upgrade.sh and lib/rollback.sh are — the top-level script owns process orchestration
# (invoking install-debian.sh/update-debian.sh/uninstall-debian.sh as subprocesses); everything
# here is pure string/file handling so it can be exercised by scripts/test-certify-release.sh
# without root, apt, systemd, or a real PostgreSQL instance.
#
# Every script that sources this must itself set `set -euo pipefail` (see lib/common.sh's own
# note — this file doesn't set it globally so it can be sourced from an already-configured shell).

# --- Certification ID ---
# Same shape as st_up_generate_upgrade_id / st_rb_generate_rollback_id.
st_cert_generate_id() {
    printf 'certify-%s-%s\n' "$(date -u '+%Y%m%d-%H%M%S')" "$(openssl rand -hex 3)"
}

# --- Logging ---
# Mirrors st_up_log's implementation exactly (terminal + best-effort append to its own log file,
# never fatal if the log directory isn't writable yet — e.g. before root setup completes).
st_cert_log() {
    install -d -m 750 -o root -g root "$SILVERTASK_UPGRADE_LOG_DIR" 2>/dev/null || true
    local line
    line="[$(date -u '+%Y-%m-%dT%H:%M:%SZ')] $*"
    echo "$line"
    if [ -w "$SILVERTASK_UPGRADE_LOG_DIR" ] || [ -w "$SILVERTASK_CERTIFICATION_LOG_FILE" ] 2>/dev/null; then
        echo "$line" >> "$SILVERTASK_CERTIFICATION_LOG_FILE" 2>/dev/null || true
        chmod 640 "$SILVERTASK_CERTIFICATION_LOG_FILE" 2>/dev/null || true
    fi
}

# --- Report file path ---
st_cert_report_path() {
    local candidate="$1" cert_id="$2"
    printf '%s/certification-%s-%s.jsonl' "$SILVERTASK_CERTIFICATION_DIR" "$candidate" "$cert_id"
}

# --- Report: run-metadata line (first line of the report) ---
st_cert_report_start() {
    local report_path="$1" cert_id="$2" candidate="$3" baseline="$4" channel="$5"
    install -d -m 750 -o root -g root "$SILVERTASK_CERTIFICATION_DIR" 2>/dev/null || true
    printf '{"type":"run","certId":"%s","candidate":"%s","baseline":"%s","channel":"%s","startedAtUtc":"%s"}\n' \
        "$(st_json_escape "$cert_id")" \
        "$(st_json_escape "$candidate")" \
        "$(st_json_escape "$baseline")" \
        "$(st_json_escape "$channel")" \
        "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" \
        > "$report_path"
    chmod 640 "$report_path" 2>/dev/null || true
}

# --- Report: one line per lifecycle stage ---
# status must be PASS, FAIL, or SKIPPED — never anything else, so st_cert_verdict below can trust
# it without re-validating.
st_cert_report_stage() {
    local report_path="$1" name="$2" status="$3" exit_code="$4" detail="${5:-}"
    printf '{"type":"stage","name":"%s","status":"%s","exitCode":"%s","detail":"%s","recordedAtUtc":"%s"}\n' \
        "$(st_json_escape "$name")" \
        "$(st_json_escape "$status")" \
        "$(st_json_escape "$exit_code")" \
        "$(st_json_escape "$detail")" \
        "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" \
        >> "$report_path"
}

# --- Report: final verdict line ---
st_cert_report_finish() {
    local report_path="$1" verdict="$2" failed_stage="${3:-}"
    printf '{"type":"verdict","verdict":"%s","failedStage":"%s","completedAtUtc":"%s"}\n' \
        "$(st_json_escape "$verdict")" \
        "$(st_json_escape "$failed_stage")" \
        "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" \
        >> "$report_path"
}

# --- Verdict computation ---
# Reads back every "stage" line already written to the report and decides CERTIFIED/NOT_CERTIFIED.
# A SKIPPED stage (currently only ever the optional cleanup stage) never blocks certification — only
# a FAIL does. Sets ST_CERT_VERDICT and ST_CERT_FAILED_STAGE (empty when CERTIFIED).
st_cert_compute_verdict() {
    local report_path="$1"
    ST_CERT_VERDICT="CERTIFIED"
    ST_CERT_FAILED_STAGE=""
    [ -f "$report_path" ] || { ST_CERT_VERDICT="NOT_CERTIFIED"; ST_CERT_FAILED_STAGE="report missing"; return 1; }

    local line name status
    while IFS= read -r line; do
        [ -n "$line" ] || continue
        case "$line" in
            '{"type":"stage"'*) ;;
            *) continue ;;
        esac
        name="$(printf '%s' "$line" | sed -n 's/.*"name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
        status="$(printf '%s' "$line" | sed -n 's/.*"status"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
        if [ "$status" = "FAIL" ] && [ -z "$ST_CERT_FAILED_STAGE" ]; then
            ST_CERT_VERDICT="NOT_CERTIFIED"
            ST_CERT_FAILED_STAGE="$name"
        fi
    done < "$report_path"

    [ "$ST_CERT_VERDICT" = "CERTIFIED" ]
}

# --- Report: read back for display (--history-style summary, used by certify-release.sh's own
# final report and available for a future --show-report style command without duplicating parsing
# logic in two places). ---
st_cert_report_read_stages() {
    local report_path="$1"
    [ -f "$report_path" ] || return 1
    grep '"type":"stage"' "$report_path"
}
