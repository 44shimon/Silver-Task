#!/bin/bash
# Silver Task — performance & scalability testing (Phase 60).
#
# Portable metric-model/reporting logic for scripts/test-performance.sh, split out the same way
# lib/certify.sh is (Phase 57) — the top-level script owns orchestration (curl calls, concurrency,
# the test-environment safety gate); everything here is pure string/file handling.
#
# Every script that sources this must itself set `set -euo pipefail` (see lib/common.sh's own
# note — this file doesn't set it globally so it can be sourced from an already-configured shell).

# --- Run ID ---
# Same shape as st_up_generate_upgrade_id / st_cert_generate_id.
st_perf_generate_id() {
    printf 'perf-%s-%s\n' "$(date -u '+%Y%m%d-%H%M%S')" "$(openssl rand -hex 3)"
}

# --- Logging ---
# Mirrors st_cert_log's implementation exactly.
st_perf_log() {
    install -d -m 750 -o root -g root "$SILVERTASK_UPGRADE_LOG_DIR" 2>/dev/null || true
    local line
    line="[$(date -u '+%Y-%m-%dT%H:%M:%SZ')] $*"
    echo "$line"
    if [ -w "$SILVERTASK_UPGRADE_LOG_DIR" ] || [ -w "$SILVERTASK_PERFORMANCE_LOG_FILE" ] 2>/dev/null; then
        echo "$line" >> "$SILVERTASK_PERFORMANCE_LOG_FILE" 2>/dev/null || true
        chmod 640 "$SILVERTASK_PERFORMANCE_LOG_FILE" 2>/dev/null || true
    fi
}

# --- Targets (scripts/perf-targets.conf) ---
# Populates the ST_PERF_TARGET_* associative array from the plain key=value conf file — same
# sed-based parsing convention every env/conf file in this codebase already uses, no new parsing
# dependency. Safe to call more than once (idempotent re-read).
declare -gA ST_PERF_TARGET
st_perf_load_targets() {
    local conf_file="$1"
    ST_PERF_TARGET=()
    [ -f "$conf_file" ] || return 1

    local line key value
    while IFS= read -r line; do
        line="${line%%#*}"
        [ -n "${line//[[:space:]]/}" ] || continue
        key="${line%%=*}"
        value="${line#*=}"
        key="$(printf '%s' "$key" | sed 's/[[:space:]]*$//')"
        value="$(printf '%s' "$value" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"
        [ -n "$key" ] && ST_PERF_TARGET["$key"]="$value"
    done < "$conf_file"
}

# --- Classification ---
# Looks up <operation>.<dataset>.<band>_ms, falling back to <operation>.<band>_ms, falling back to
# default.<band>_ms — see perf-targets.conf's own header comment for the exact lookup order. Sets
# ST_PERF_VERDICT to FAST/ACCEPTABLE/WARNING/SLOW; never fails (a target that's missing everywhere,
# even the global default, is treated as ACCEPTABLE rather than crashing the whole test run over a
# config gap).
st_perf_classify() {
    local operation="$1" dataset="$2" duration_ms="$3"
    local fast acceptable warning

    fast="$(st_perf_lookup_target "$operation" "$dataset" "fast_ms")"
    acceptable="$(st_perf_lookup_target "$operation" "$dataset" "acceptable_ms")"
    warning="$(st_perf_lookup_target "$operation" "$dataset" "warning_ms")"
    fast="${fast:-200}"; acceptable="${acceptable:-500}"; warning="${warning:-1500}"

    if [ "$duration_ms" -le "$fast" ]; then
        ST_PERF_VERDICT="FAST"
    elif [ "$duration_ms" -le "$acceptable" ]; then
        ST_PERF_VERDICT="ACCEPTABLE"
    elif [ "$duration_ms" -le "$warning" ]; then
        ST_PERF_VERDICT="WARNING"
    else
        ST_PERF_VERDICT="SLOW"
    fi
}

st_perf_lookup_target() {
    local operation="$1" dataset="$2" band="$3"
    if [ -n "${ST_PERF_TARGET["$operation.$dataset.$band"]:-}" ]; then
        printf '%s' "${ST_PERF_TARGET["$operation.$dataset.$band"]}"
    elif [ -n "${ST_PERF_TARGET["$operation.$band"]:-}" ]; then
        printf '%s' "${ST_PERF_TARGET["$operation.$band"]}"
    else
        printf '%s' "${ST_PERF_TARGET["default.$band"]:-}"
    fi
}

# --- Report file path (per-run detailed report — distinct from the durable history file below) ---
st_perf_report_path() {
    local dataset="$1" profile="$2" run_id="$3"
    printf '%s/performance-%s-%s-%s.jsonl' "$SILVERTASK_PERFORMANCE_DIR" "$dataset" "$profile" "$run_id"
}

# --- Report: run-metadata line (first line of the report) ---
st_perf_report_start() {
    local report_path="$1" run_id="$2" version="$3" environment="$4" dataset="$5" profile="$6"
    install -d -m 750 -o root -g root "$SILVERTASK_PERFORMANCE_DIR" 2>/dev/null || true
    printf '{"type":"run","runId":"%s","version":"%s","environment":"%s","datasetSize":"%s","profile":"%s","startedAtUtc":"%s"}\n' \
        "$(st_json_escape "$run_id")" \
        "$(st_json_escape "$version")" \
        "$(st_json_escape "$environment")" \
        "$(st_json_escape "$dataset")" \
        "$(st_json_escape "$profile")" \
        "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" \
        > "$report_path"
    chmod 640 "$report_path" 2>/dev/null || true
}

# --- Report: one line per measured operation ---
# Never logs request bodies, task content, or any identifying data — operation name + timing +
# outcome + response size only, per the spec's own explicit "do not collect private task content
# merely for performance testing." Classifies against perf-targets.conf itself (st_perf_load_targets
# must already have been called) so the report is self-contained — a reader never has to
# cross-reference the conf file separately to know whether a number was good or bad at the time of
# the run. payload_bytes is optional (omit/pass empty for operations where response size isn't
# meaningful, e.g. a 204 No Content write) — recorded as 0 when not given, never null (keeps every
# operation line the same shape for simpler downstream parsing).
st_perf_record_operation() {
    local report_path="$1" operation="$2" dataset="$3" duration_ms="$4" success="$5" payload_bytes="${6:-0}"
    local verdict="TIMEOUT_OR_ERROR"
    if [ "$success" = "true" ]; then
        st_perf_classify "$operation" "$dataset" "$duration_ms"
        verdict="$ST_PERF_VERDICT"
    fi
    printf '{"type":"operation","operation":"%s","datasetSize":"%s","durationMs":%s,"payloadBytes":%s,"success":%s,"verdict":"%s","timestamp":"%s"}\n' \
        "$(st_json_escape "$operation")" \
        "$(st_json_escape "$dataset")" \
        "$duration_ms" \
        "${payload_bytes:-0}" \
        "$success" \
        "$(st_json_escape "$verdict")" \
        "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" \
        >> "$report_path"
}

# --- Report: final verdict line ---
st_perf_report_finish() {
    local report_path="$1" final_result="$2" warning_count="$3" slow_count="$4" failure_count="$5"
    printf '{"type":"summary","finalResult":"%s","warnings":%s,"slow":%s,"failures":%s,"completedAtUtc":"%s"}\n' \
        "$(st_json_escape "$final_result")" \
        "$warning_count" "$slow_count" "$failure_count" \
        "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" \
        >> "$report_path"
}

# --- Durable, cross-run version performance history — deliberately separate from the detailed
# per-run report above (which holds every individual operation measurement): one compact summary
# line per completed run, so trends across versions can be read without holding every raw
# measurement ever taken. operations_json must be a caller-built, already-valid compact JSON object
# string (e.g. '{"login":142,"dashboard":310}') — kept to KEY operations only, never the full report,
# per the spec's own "do not store unlimited detailed raw data." ---
st_perf_history_append() {
    local version="$1" dataset="$2" profile="$3" final_result="$4" operations_json="$5"
    install -d -m 750 -o root -g root "$SILVERTASK_PERFORMANCE_DIR" 2>/dev/null || true
    printf '{"version":"%s","datasetSize":"%s","profile":"%s","finalResult":"%s","operations":%s,"timestamp":"%s"}\n' \
        "$(st_json_escape "$version")" \
        "$(st_json_escape "$dataset")" \
        "$(st_json_escape "$profile")" \
        "$(st_json_escape "$final_result")" \
        "$operations_json" \
        "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" \
        >> "$SILVERTASK_PERFORMANCE_HISTORY_FILE"
    chmod 640 "$SILVERTASK_PERFORMANCE_HISTORY_FILE" 2>/dev/null || true
}

st_perf_history_read() {
    local limit="${1:-20}"
    [ -f "$SILVERTASK_PERFORMANCE_HISTORY_FILE" ] || return 1
    tail -n "$limit" "$SILVERTASK_PERFORMANCE_HISTORY_FILE" | tac
}
