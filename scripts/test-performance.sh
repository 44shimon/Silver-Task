#!/bin/bash
# Silver Task — performance, scalability & load testing (Phase 60).
#
# Measures real operation timings against a running instance (login, dashboard, my-tasks, project
# sheet load, task create/update/delete, search, filter, project switch, and the Kanban/Calendar/
# Timeline/Gantt/admin-users equivalents — see docs/performance.md for exactly what each measures
# and why), classifies each against scripts/perf-targets.conf (FAST/ACCEPTABLE/WARNING/SLOW), and
# reports PASS/WARNING/SLOW/FAIL — human-readable by default, --json for machine-readable output.
#
# Reuses scripts/lib/perf.sh for the JSON Lines report/history model (same pattern
# lib/certify.sh established in Phase 57) and the dev-only PerformanceDataSeeder
# (`dotnet run -- --perf-seed=<dataset>`) for the synthetic project this measures the Sheet/
# Kanban/Calendar/Timeline/Gantt-equivalent load against.
#
# ****************************************************************************************
# * THIS SCRIPT SENDS REAL HTTP REQUESTS — INCLUDING WRITES (task create/update/delete) IN *
# * THE normal/heavy PROFILES — TO WHATEVER --base-url POINTS AT. --target-env IS REQUIRED  *
# * WITH NO DEFAULT SPECIFICALLY SO THIS CAN NEVER ACCIDENTALLY RUN AGAINST PRODUCTION.     *
# ****************************************************************************************
#
# Usage:
#   ./scripts/test-performance.sh --target-env=development --dataset=small --profile=smoke
#   ./scripts/test-performance.sh --target-env=development --dataset=large --profile=heavy --json
#   ./scripts/test-performance.sh --help
#
# --target-env=development|test   (required) explicit acknowledgment this is not production.
# --base-url=URL                  Default: http://127.0.0.1:5000
# --dataset=small|medium|large    Default: small. Must match a dataset already seeded via
#                                  `dotnet run -- --perf-seed=<dataset>` for project-scoped
#                                  operations to measure anything real.
# --profile=smoke|normal|heavy    Default: smoke. smoke=1 simulated user, read-only. normal=5
#                                  concurrent, read+write. heavy=20 concurrent, read+write.
# --concurrency=N                 Overrides the profile's default concurrency.
# --user-email=/--user-password=  Default: the PerformanceDataSeeder-seeded project owner for
#                                  --dataset (perf-test-<dataset>-user-0001@example.invalid /
#                                  PerfTest1234!).
# --admin-email=/--admin-password= Default: the DemoDataSeeder-seeded Administrator
#                                  (admin@example.com / Demo1234!) — only used for the
#                                  admin-users-list measurement, which requires that role.
# --json                          Machine-readable structured output instead of the human report.
# --yes                           Skip the typed confirmation normal/heavy profiles otherwise
#                                  require before performing writes.
# --help, -h                      Show this message.
#
# Exit codes: 0 completed (FAST/ACCEPTABLE/WARNING/SLOW are all still exit 0 — see
# docs/performance.md "why a slow result isn't a failure"), 1 general error, 2 invalid arguments
# or the test-environment gate was not satisfied, 3 one or more operations genuinely failed or
# timed out (a real error, not just slow).

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"
# shellcheck source=lib/perf.sh
source "$SCRIPT_DIR/lib/perf.sh"

st_perf_usage() {
    cat <<'EOF'
Usage: ./scripts/test-performance.sh --target-env=development|test [options]

  --target-env=development|test   Required. Explicit acknowledgment this is not production.
  --base-url=URL                  Default: http://127.0.0.1:5000
  --dataset=small|medium|large    Default: small. Seed it first: dotnet run -- --perf-seed=<size>
  --profile=smoke|normal|heavy    Default: smoke.
  --concurrency=N                 Overrides the profile's default concurrency.
  --user-email=/--user-password=  Default: the seeded perf-test project owner.
  --admin-email=/--admin-password= Default: the seeded demo Administrator (admin-users list only).
  --json                          Machine-readable structured output.
  --yes                           Skip the typed confirmation normal/heavy profiles require.
  --help, -h                      Show this message.

See docs/performance.md for the full metric model, targets, and safety requirements.
EOF
}

TARGET_ENV=""
BASE_URL="http://127.0.0.1:5000"
DATASET="small"
PROFILE="smoke"
CONCURRENCY=""
USER_EMAIL=""
USER_PASSWORD="PerfTest1234!"
ADMIN_EMAIL="admin@example.com"
ADMIN_PASSWORD="Demo1234!"
JSON_OUTPUT=false
ASSUME_YES=false
REQUEST_TIMEOUT=15

while [ $# -gt 0 ]; do
    case "$1" in
        --target-env=*) TARGET_ENV="${1#*=}" ;;
        --base-url=*) BASE_URL="${1#*=}" ;;
        --dataset=*) DATASET="${1#*=}" ;;
        --profile=*) PROFILE="${1#*=}" ;;
        --concurrency=*) CONCURRENCY="${1#*=}" ;;
        --user-email=*) USER_EMAIL="${1#*=}" ;;
        --user-password=*) USER_PASSWORD="${1#*=}" ;;
        --admin-email=*) ADMIN_EMAIL="${1#*=}" ;;
        --admin-password=*) ADMIN_PASSWORD="${1#*=}" ;;
        --json) JSON_OUTPUT=true ;;
        --yes) ASSUME_YES=true ;;
        --help|-h) st_perf_usage; exit 0 ;;
        *) st_perf_usage >&2; echo "ERROR: Unknown argument: $1" >&2; exit 2 ;;
    esac
    shift
done

# --- Test environment protection (mandatory, no default — see header) ---
if [ "$TARGET_ENV" != "development" ] && [ "$TARGET_ENV" != "test" ]; then
    echo "PERFORMANCE TEST BLOCKED — TEST ENVIRONMENT NOT VERIFIED" >&2
    echo "" >&2
    echo "--target-env=development or --target-env=test is required and was not provided (or was" >&2
    echo "not one of those two values). This script sends real HTTP requests — including writes" >&2
    echo "in the normal/heavy profiles — and must never run against production by accident." >&2
    exit 2
fi

if [ "$DATASET" != "small" ] && [ "$DATASET" != "medium" ] && [ "$DATASET" != "large" ]; then
    st_perf_usage >&2; echo "ERROR: --dataset must be small, medium, or large." >&2; exit 2
fi
if [ "$PROFILE" != "smoke" ] && [ "$PROFILE" != "normal" ] && [ "$PROFILE" != "heavy" ]; then
    st_perf_usage >&2; echo "ERROR: --profile must be smoke, normal, or heavy." >&2; exit 2
fi

# User emails are profile/dataset-scoped (perf-test-<dataset>-user-NNNN@example.invalid) — must
# stay in sync with Silver-Task.Server/Data/Seeding/PerformanceDataSeeder.cs's Profiles dict.
case "$DATASET" in
    small) DATASET_USER_COUNT=10 ;;
    medium) DATASET_USER_COUNT=25 ;;
    large) DATASET_USER_COUNT=50 ;;
esac
if [ -z "$USER_EMAIL" ]; then
    USER_EMAIL="perf-test-${DATASET}-user-0001@example.invalid"
fi

case "$PROFILE" in
    smoke) DEFAULT_CONCURRENCY=1; PERFORMS_WRITES=false ;;
    normal) DEFAULT_CONCURRENCY=5; PERFORMS_WRITES=true ;;
    heavy) DEFAULT_CONCURRENCY=20; PERFORMS_WRITES=true ;;
esac
CONCURRENCY="${CONCURRENCY:-$DEFAULT_CONCURRENCY}"
if ! [[ "$CONCURRENCY" =~ ^[0-9]+$ ]] || [ "$CONCURRENCY" -lt 1 ]; then
    st_perf_usage >&2; echo "ERROR: --concurrency must be a positive integer." >&2; exit 2
fi

if [ "$PERFORMS_WRITES" = true ] && [ "$ASSUME_YES" != true ]; then
    echo ""
    echo "WARNING: the $PROFILE profile creates, updates, and deletes real task rows against"
    echo "$BASE_URL (target-env: $TARGET_ENV) as part of measuring write performance."
    if ! st_confirm_destructive "About to run a write-performing performance test against $BASE_URL." "$TARGET_ENV"; then
        echo "Aborted by administrator (write-profile confirmation not confirmed)."
        exit 0
    fi
fi

VERSION="$(curl -fsS --max-time 5 "$BASE_URL/api/health" 2>/dev/null | sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
VERSION="${VERSION:-unknown}"

RUN_ID="$(st_perf_generate_id)"
REPORT_PATH="$(st_perf_report_path "$DATASET" "$PROFILE" "$RUN_ID")"
st_perf_load_targets "$SCRIPT_DIR/perf-targets.conf" || true
st_perf_report_start "$REPORT_PATH" "$RUN_ID" "$VERSION" "$TARGET_ENV" "$DATASET" "$PROFILE"
st_perf_log "test-performance: starting (id=$RUN_ID dataset=$DATASET profile=$PROFILE concurrency=$CONCURRENCY target-env=$TARGET_ENV base-url=$BASE_URL)"

WARN_COUNT=0
SLOW_COUNT=0
FAIL_COUNT=0
RATE_LIMIT_COUNT=0
declare -A KEY_DURATIONS

# --- HTTP helper: sets TR_STATUS, TR_DURATION_MS, TR_SIZE_BYTES, TR_BODY (body written to a fresh
# temp file per call, read into TR_BODY, then removed) ---
st_perf_request() {
    local jar="$1" method="$2" url="$3" data="${4:-}"
    local body_file out time_s
    body_file="$(mktemp)"
    if [ -n "$data" ]; then
        out="$(curl -s -o "$body_file" -w '%{http_code} %{time_total} %{size_download}' --max-time "$REQUEST_TIMEOUT" \
            -b "$jar" -c "$jar" -X "$method" -H "Content-Type: application/json" -d "$data" "$url" 2>/dev/null)" || out="000 0 0"
    else
        out="$(curl -s -o "$body_file" -w '%{http_code} %{time_total} %{size_download}' --max-time "$REQUEST_TIMEOUT" \
            -b "$jar" -c "$jar" -X "$method" "$url" 2>/dev/null)" || out="000 0 0"
    fi
    TR_STATUS="$(printf '%s' "$out" | awk '{print $1}')"
    time_s="$(printf '%s' "$out" | awk '{print $2}')"
    TR_SIZE_BYTES="$(printf '%s' "$out" | awk '{print $3}')"
    TR_DURATION_MS="$(awk -v t="$time_s" 'BEGIN { printf "%.0f", (t+0) * 1000 }')"
    TR_BODY="$(cat "$body_file" 2>/dev/null)"
    rm -f "$body_file"
}

# --- Records one measurement, updates the running warn/slow/fail counters, and (for the handful
# of "key" operations) remembers the duration for the durable history summary. ---
record() {
    local operation="$1" success="$2" duration_ms="$3" is_key="${4:-false}" payload_bytes="${5:-0}"
    st_perf_record_operation "$REPORT_PATH" "$operation" "$DATASET" "$duration_ms" "$success" "$payload_bytes"
    if [ "$success" != "true" ]; then
        FAIL_COUNT=$((FAIL_COUNT + 1))
        echo "  FAIL       $operation (request failed or timed out)"
        return
    fi
    case "$ST_PERF_VERDICT" in
        WARNING) WARN_COUNT=$((WARN_COUNT + 1)) ;;
        SLOW) SLOW_COUNT=$((SLOW_COUNT + 1)) ;;
    esac
    if [ "$payload_bytes" -gt 0 ] 2>/dev/null; then
        printf '  %-10s %-20s %5sms  %8s bytes\n' "$ST_PERF_VERDICT" "$operation" "$duration_ms" "$payload_bytes"
    else
        printf '  %-10s %-20s %5sms\n' "$ST_PERF_VERDICT" "$operation" "$duration_ms"
    fi
    if [ "$is_key" = true ]; then
        KEY_DURATIONS["$operation"]="$duration_ms"
    fi
}

# --- A 429 on a login attempt is Security:LoginRateLimit (Phase 59) doing its job, not an
# application defect — every simulated user in a concurrency pass logs in from this one test
# machine's IP, which the rate limiter can't distinguish from a single real client. Reported
# distinctly (never silently dropped) and counted separately from FAIL_COUNT so it doesn't read
# as a backend bug. See docs/performance-runbook.md. ---
record_rate_limited() {
    local operation="$1"
    RATE_LIMIT_COUNT=$((RATE_LIMIT_COUNT + 1))
    st_perf_record_operation "$REPORT_PATH" "$operation" "$DATASET" 0 false 0
    echo "  RATE-LIMIT $operation (429 from Security:LoginRateLimit — see docs/performance-runbook.md)"
}

echo "Silver Task Performance Test"
echo "Version: $VERSION | Environment: $TARGET_ENV | Dataset: $DATASET | Profile: $PROFILE (concurrency: $CONCURRENCY)"
echo "Base URL: $BASE_URL"
echo "Report: $REPORT_PATH"
echo ""

# --- Login (also establishes the session cookie every later call in this sequential pass reuses) ---
USER_JAR="$(mktemp)"
st_perf_request "$USER_JAR" POST "$BASE_URL/api/auth/login" "{\"email\":\"$USER_EMAIL\",\"password\":\"$USER_PASSWORD\"}"
if [ "$TR_STATUS" = "200" ]; then
    record "login" true "$TR_DURATION_MS" true
elif [ "$TR_STATUS" = "429" ]; then
    record_rate_limited "login"
    st_perf_log "test-performance: BLOCKED, login rate-limited (429) for $USER_EMAIL"
    echo "" >&2
    echo "Login for $USER_EMAIL was rate-limited (429) — Security:LoginRateLimit (default 10" >&2
    echo "requests/60s, IP-partitioned) was already exhausted by earlier requests from this" >&2
    echo "machine. Wait for the window to reset, or run against a target with a raised" >&2
    echo "Security__LoginRateLimit__PermitLimit. See docs/performance-runbook.md." >&2
    rm -f "$USER_JAR"
    st_perf_report_finish "$REPORT_PATH" "RATE_LIMITED" "$WARN_COUNT" "$SLOW_COUNT" "$((FAIL_COUNT))"
    exit 3
else
    record "login" false "$TR_DURATION_MS"
    st_perf_log "test-performance: FAILED, could not log in as $USER_EMAIL (status $TR_STATUS)"
    echo "" >&2
    echo "Could not log in as $USER_EMAIL — nothing else can be measured. Seed this dataset first:" >&2
    echo "  dotnet run --project Silver-Task.Server -- --perf-seed=$DATASET" >&2
    rm -f "$USER_JAR"
    st_perf_report_finish "$REPORT_PATH" "FAILED" "$WARN_COUNT" "$SLOW_COUNT" "$((FAIL_COUNT))"
    exit 3
fi

# Resolve the seeded perf-test project's id — every project-scoped measurement below needs it.
st_perf_request "$USER_JAR" GET "$BASE_URL/api/projects"
PROJECT_ID="$(printf '%s' "$TR_BODY" | grep -oE '"id":"[^"]*","name":"\[Perf Test\] '"$(printf '%s' "$DATASET" | tr '[:lower:]' '[:upper:]')"' Dataset"' | head -1 | sed -n 's/"id":"\([^"]*\)".*/\1/p')"
if [ -z "$PROJECT_ID" ]; then
    echo "" >&2
    echo "WARNING: could not find the seeded \"[Perf Test] $(printf '%s' "$DATASET" | tr '[:lower:]' '[:upper:]') Dataset\" project." >&2
    echo "Project-scoped measurements (sheet/kanban/calendar/timeline/gantt/project_switch) will be skipped." >&2
    echo "Seed it first: dotnet run --project Silver-Task.Server -- --perf-seed=$DATASET" >&2
fi

# --- Dashboard / My Tasks ---
st_perf_request "$USER_JAR" GET "$BASE_URL/api/dashboard"
record "dashboard" "$([ "$TR_STATUS" = "200" ] && echo true || echo false)" "$TR_DURATION_MS" true "$TR_SIZE_BYTES"

st_perf_request "$USER_JAR" GET "$BASE_URL/api/tasks/my"
record "my_tasks" "$([ "$TR_STATUS" = "200" ] && echo true || echo false)" "$TR_DURATION_MS" true "$TR_SIZE_BYTES"

if [ -n "$PROJECT_ID" ]; then
    # --- Project sheet load — also what Kanban/Calendar/Timeline/Gantt each load: confirmed by
    # the Phase 60 audit that none of the four has a separate backend endpoint, they're pure
    # frontend layouts over this exact same response. Measured once, reported under all five names
    # since that's what a human report reader expects to see accounted for. ---
    st_perf_request "$USER_JAR" GET "$BASE_URL/api/projects/$PROJECT_ID/tasks"
    sheet_success="$([ "$TR_STATUS" = "200" ] && echo true || echo false)"
    sheet_duration="$TR_DURATION_MS"
    sheet_size="$TR_SIZE_BYTES"
    record "project_sheet" "$sheet_success" "$sheet_duration" true "$sheet_size"
    for view in kanban calendar timeline gantt; do
        record "$view" "$sheet_success" "$sheet_duration" false "$sheet_size"
    done

    st_perf_request "$USER_JAR" GET "$BASE_URL/api/projects/$PROJECT_ID"
    record "project_switch" "$([ "$TR_STATUS" = "200" ] && echo true || echo false)" "$TR_DURATION_MS" false "$TR_SIZE_BYTES"
fi

# --- Search (common / rare / empty terms) ---
st_perf_request "$USER_JAR" GET "$BASE_URL/api/search?q=Task"
record "search_common" "$([ "$TR_STATUS" = "200" ] && echo true || echo false)" "$TR_DURATION_MS" true "$TR_SIZE_BYTES"

st_perf_request "$USER_JAR" GET "$BASE_URL/api/search?q=00001"
record "search_rare" "$([ "$TR_STATUS" = "200" ] && echo true || echo false)" "$TR_DURATION_MS" false "$TR_SIZE_BYTES"

st_perf_request "$USER_JAR" GET "$BASE_URL/api/search?q=zzz-no-such-task-zzz"
record "search_empty" "$([ "$TR_STATUS" = "200" ] && echo true || echo false)" "$TR_DURATION_MS" false "$TR_SIZE_BYTES"

# --- Filter — the one place server-side filtering genuinely exists (Search's status/priority
# params). The Sheet view's own filter/sort/search is entirely client-side (confirmed by the
# Phase 60 audit) and therefore not something HTTP timing can measure at all — see
# docs/performance.md for that limitation stated plainly, not glossed over. ---
st_perf_request "$USER_JAR" GET "$BASE_URL/api/search?q=Task&status=NotStarted"
record "filter" "$([ "$TR_STATUS" = "200" ] && echo true || echo false)" "$TR_DURATION_MS" true "$TR_SIZE_BYTES"

# --- Writes (normal/heavy only) ---
if [ "$PERFORMS_WRITES" = true ] && [ -n "$PROJECT_ID" ]; then
    st_perf_request "$USER_JAR" POST "$BASE_URL/api/projects/$PROJECT_ID/tasks" \
        '{"title":"Perf test write-timing task","description":"Created by test-performance.sh; deleted at the end of this run."}'
    create_success="$([ "$TR_STATUS" = "201" ] && echo true || echo false)"
    record "task_create" "$create_success" "$TR_DURATION_MS" true
    NEW_TASK_ID="$(printf '%s' "$TR_BODY" | sed -n 's/^{"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"

    if [ -n "${NEW_TASK_ID:-}" ]; then
        st_perf_request "$USER_JAR" PUT "$BASE_URL/api/tasks/$NEW_TASK_ID" \
            '{"title":"Perf test write-timing task (updated)","description":"Updated by test-performance.sh.","status":"InProgress","priority":"Medium"}'
        record "task_update" "$([ "$TR_STATUS" = "200" ] && echo true || echo false)" "$TR_DURATION_MS" true

        st_perf_request "$USER_JAR" DELETE "$BASE_URL/api/tasks/$NEW_TASK_ID"
        record "task_delete" "$([ "$TR_STATUS" = "204" ] && echo true || echo false)" "$TR_DURATION_MS" true
    fi
fi

rm -f "$USER_JAR"

# --- Admin user list (needs the Administrator credential — separate login) ---
ADMIN_JAR="$(mktemp)"
st_perf_request "$ADMIN_JAR" POST "$BASE_URL/api/auth/login" "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}"
if [ "$TR_STATUS" = "200" ]; then
    st_perf_request "$ADMIN_JAR" GET "$BASE_URL/api/users"
    record "admin_users" "$([ "$TR_STATUS" = "200" ] && echo true || echo false)" "$TR_DURATION_MS" true "$TR_SIZE_BYTES"
elif [ "$TR_STATUS" = "429" ]; then
    record_rate_limited "admin_login"
else
    echo "  SKIPPED    admin_users (could not log in as $ADMIN_EMAIL — see --admin-email/--admin-password)"
fi
rm -f "$ADMIN_JAR"

# --- Concurrency (normal/heavy): re-run a representative read (+write) mix across $CONCURRENCY
# distinct seeded perf-test users at once, via bash background jobs — this codebase's own
# established "no new external dependency" pattern (no k6/JMeter/Locust available or justified
# here). Each worker writes its own timing lines to a private temp file (background jobs can't
# share shell variables with the parent); the parent reads them all back after `wait`. ---
if [ "$CONCURRENCY" -gt 1 ]; then
    echo ""
    echo "Concurrency pass: $CONCURRENCY simulated users..."
    CONCURRENCY_DIR="$(mktemp -d)"

    st_perf_worker() {
        local worker_index="$1" out_file="$2"
        local email="perf-test-${DATASET}-user-$(printf '%04d' "$worker_index")@example.invalid"
        local jar body_file out time_s status
        jar="$(mktemp)"

        local start_epoch end_epoch
        start_epoch=$(date +%s%N)
        out="$(curl -s -o /dev/null -w '%{http_code} %{time_total}' --max-time "$REQUEST_TIMEOUT" \
            -c "$jar" -X POST -H "Content-Type: application/json" \
            -d "{\"email\":\"$email\",\"password\":\"$USER_PASSWORD\"}" "$BASE_URL/api/auth/login" 2>/dev/null)" || out="000 0"
        status="${out%% *}"
        time_s="${out##* }"
        if [ "$status" = "200" ]; then
            echo "login true $(awk -v t="$time_s" 'BEGIN{printf "%.0f", (t+0)*1000}')" >> "$out_file"

            for endpoint in "/api/dashboard" "/api/tasks/my"; do
                out="$(curl -s -o /dev/null -w '%{http_code} %{time_total}' --max-time "$REQUEST_TIMEOUT" -b "$jar" "$BASE_URL$endpoint" 2>/dev/null)" || out="000 0"
                status="${out%% *}"; time_s="${out##* }"
                local op="dashboard"; [ "$endpoint" = "/api/tasks/my" ] && op="my_tasks"
                echo "$op $([ "$status" = "200" ] && echo true || echo false) $(awk -v t="$time_s" 'BEGIN{printf "%.0f", (t+0)*1000}')" >> "$out_file"
            done
        elif [ "$status" = "429" ]; then
            # Security:LoginRateLimit (Phase 59) — every worker logs in from this one test
            # machine's IP, which the rate limiter treats as one client. Real information, not a
            # backend defect — flagged distinctly so the parent doesn't fold it into FAIL_COUNT.
            echo "login rate_limited 0" >> "$out_file"
        else
            echo "login false 0" >> "$out_file"
        fi
        rm -f "$jar"
        end_epoch=$(date +%s%N)
        : "$start_epoch" "$end_epoch"  # available for future wall-clock-per-worker reporting
    }

    for ((i = 1; i <= CONCURRENCY; i++)); do
        # Cycle through however many perf-test users the seeded dataset actually has, so a
        # --concurrency higher than the dataset's own user count still works (reuses users
        # rather than failing to log in as one that doesn't exist).
        worker_user_index=$(( ((i - 1) % DATASET_USER_COUNT) + 1 ))
        st_perf_worker "$worker_user_index" "$CONCURRENCY_DIR/worker-$i.txt" &
    done
    wait

    for f in "$CONCURRENCY_DIR"/worker-*.txt; do
        [ -f "$f" ] || continue
        while IFS=' ' read -r op success duration_ms; do
            [ -n "${op:-}" ] || continue
            if [ "$success" = "rate_limited" ]; then
                record_rate_limited "concurrent_$op"
                continue
            fi
            record "concurrent_$op" "$success" "$duration_ms"
        done < "$f"
    done
    rm -rf "$CONCURRENCY_DIR"
fi

# --- Final result ---
if [ "$FAIL_COUNT" -gt 0 ]; then
    FINAL_RESULT="FAILED"
elif [ "$SLOW_COUNT" -gt 0 ]; then
    FINAL_RESULT="SLOW"
elif [ "$WARN_COUNT" -gt 0 ]; then
    FINAL_RESULT="WARNING"
else
    FINAL_RESULT="ACCEPTABLE"
fi

st_perf_report_finish "$REPORT_PATH" "$FINAL_RESULT" "$WARN_COUNT" "$SLOW_COUNT" "$FAIL_COUNT"

# Durable cross-version history — key operations only, not the full report (see lib/perf.sh).
operations_json="{"
first=true
for op in "${!KEY_DURATIONS[@]}"; do
    [ "$first" = true ] || operations_json+=","
    operations_json+="\"$op\":${KEY_DURATIONS[$op]}"
    first=false
done
operations_json+="}"
st_perf_history_append "$VERSION" "$DATASET" "$PROFILE" "$FINAL_RESULT" "$operations_json"

st_perf_log "test-performance: $FINAL_RESULT (warnings=$WARN_COUNT slow=$SLOW_COUNT failures=$FAIL_COUNT rate_limited=$RATE_LIMIT_COUNT)"

if [ "$JSON_OUTPUT" = true ]; then
    echo ""
    printf '{"version":"%s","environment":"%s","datasetSize":"%s","profile":"%s","warnings":%s,"slow":%s,"failures":%s,"rateLimited":%s,"finalResult":"%s","reportPath":"%s"}\n' \
        "$(st_json_escape "$VERSION")" "$(st_json_escape "$TARGET_ENV")" "$(st_json_escape "$DATASET")" "$(st_json_escape "$PROFILE")" \
        "$WARN_COUNT" "$SLOW_COUNT" "$FAIL_COUNT" "$RATE_LIMIT_COUNT" "$(st_json_escape "$FINAL_RESULT")" "$(st_json_escape "$REPORT_PATH")"
else
    echo ""
    echo "=================================================================="
    echo " FINAL RESULT: $FINAL_RESULT"
    echo " Warnings: $WARN_COUNT | Slow: $SLOW_COUNT | Failures: $FAIL_COUNT | Rate-limited: $RATE_LIMIT_COUNT"
    if [ "$RATE_LIMIT_COUNT" -gt 0 ]; then
        echo " NOTE: $RATE_LIMIT_COUNT login(s) were blocked by Security:LoginRateLimit, not a backend"
        echo " failure — see docs/performance-runbook.md. Not counted toward Failures."
    fi
    echo " Report: $REPORT_PATH"
    echo "=================================================================="
fi

[ "$FAIL_COUNT" -eq 0 ] && exit 0
exit 3
