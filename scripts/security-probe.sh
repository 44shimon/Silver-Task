#!/bin/bash
# Silver Task — live security regression probe (Phase 59).
#
# Distinct from the test-*.sh naming (test-upgrade-engine.sh/test-certify-release.sh mean "pure
# portable-logic unit tests, no live server needed") — this genuinely requires a running instance
# to probe, so it's named like certify-release.sh instead: live orchestration against a real app.
#
# Scripts a repeatable version of the manual checks Phase 47's one-time audit already did once
# (unauthenticated access, cross-role access, IDOR) so they can be re-run on demand — against a
# dev server, staging, or (read the caveats below) production.
#
# By default, uses the demo accounts scripts/../Silver-Task.Server/Data/Seeding/DemoDataSeeder.cs
# creates (--seed, Development-only, password Demo1234!). Pass --admin-email/--admin-password/
# --member-email/--member-password to run against any instance with real accounts instead.
#
# The IDOR probe creates one throwaway project as the admin account (named distinctly, deleted
# again at the end regardless of pass/fail) specifically so the probe is self-contained and never
# depends on assumptions about pre-existing project membership that could silently drift.
#
# Usage:
#   bash scripts/security-probe.sh
#   bash scripts/security-probe.sh --base-url=https://staging.example.com \
#       --admin-email=admin@example.com --admin-password='...' \
#       --member-email=member@example.com --member-password='...'
#
# Exit codes: 0 all probes passed, 1 one or more probes failed, 2 could not run (login failed,
# server unreachable — distinct from a real finding, since it means nothing was actually proven).

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

BASE_URL="http://127.0.0.1:5000"
ADMIN_EMAIL="admin@example.com"
ADMIN_PASSWORD="Demo1234!"
MEMBER_EMAIL="alice@example.com"
MEMBER_PASSWORD="Demo1234!"

st_probe_usage() {
    cat <<'EOF'
Usage: bash scripts/security-probe.sh [options]

  --base-url=URL           Default: http://127.0.0.1:5000
  --admin-email=EMAIL      Default: admin@example.com (DemoDataSeeder's seeded Administrator)
  --admin-password=PASS    Default: Demo1234!
  --member-email=EMAIL     Default: alice@example.com (DemoDataSeeder's seeded Member)
  --member-password=PASS   Default: Demo1234!
  --help, -h                Show this message.
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --base-url=*) BASE_URL="${1#*=}" ;;
        --admin-email=*) ADMIN_EMAIL="${1#*=}" ;;
        --admin-password=*) ADMIN_PASSWORD="${1#*=}" ;;
        --member-email=*) MEMBER_EMAIL="${1#*=}" ;;
        --member-password=*) MEMBER_PASSWORD="${1#*=}" ;;
        --help|-h) st_probe_usage; exit 0 ;;
        *) st_probe_usage >&2; echo "ERROR: Unknown argument: $1" >&2; exit 2 ;;
    esac
    shift
done

PASS=0
FAIL=0
PROJECT_ID=""
ADMIN_JAR="$(mktemp)"
MEMBER_JAR="$(mktemp)"

cleanup() {
    if [ -n "$PROJECT_ID" ]; then
        curl -fsS -b "$ADMIN_JAR" -X DELETE "$BASE_URL/api/projects/$PROJECT_ID" >/dev/null 2>&1 || true
    fi
    rm -f "$ADMIN_JAR" "$MEMBER_JAR"
}
trap cleanup EXIT

assert_status() {
    local desc="$1" expected="$2" actual="$3"
    if [ "$actual" = "$expected" ]; then
        st_info "[PASS] $desc (got $actual)"
        PASS=$((PASS + 1))
    else
        st_error "[FAIL] $desc (expected $expected, got $actual)"
        FAIL=$((FAIL + 1))
    fi
}

echo "Silver Task Security Probe"
echo "Base URL: $BASE_URL"
echo ""

st_step "Logging in"
login() {
    local jar="$1" email="$2" password="$3"
    curl -fsS -c "$jar" -o /dev/null -w '%{http_code}' -X POST "$BASE_URL/api/auth/login" \
        -H "Content-Type: application/json" \
        -d "{\"email\":\"$email\",\"password\":\"$password\"}"
}
admin_login_status="$(login "$ADMIN_JAR" "$ADMIN_EMAIL" "$ADMIN_PASSWORD")"
if [ "$admin_login_status" != "200" ]; then
    st_error "Could not log in as admin ($ADMIN_EMAIL) — got HTTP $admin_login_status. Nothing else can be probed."
    exit 2
fi
member_login_status="$(login "$MEMBER_JAR" "$MEMBER_EMAIL" "$MEMBER_PASSWORD")"
if [ "$member_login_status" != "200" ]; then
    st_error "Could not log in as member ($MEMBER_EMAIL) — got HTTP $member_login_status. Nothing else can be probed."
    exit 2
fi
st_info "Logged in as both admin and member accounts."
echo ""

st_step "Probe: unauthenticated access to a protected endpoint"
status="$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/api/projects")"
assert_status "GET /api/projects with no session -> 401" "401" "$status"

st_step "Probe: non-admin access to an admin-only endpoint"
status="$(curl -s -b "$MEMBER_JAR" -o /dev/null -w '%{http_code}' "$BASE_URL/api/admin/diagnostics")"
assert_status "GET /api/admin/diagnostics as a Member -> 403" "403" "$status"

st_step "Probe: IDOR (a non-member requesting a project they don't belong to)"
create_response="$(curl -fsS -b "$ADMIN_JAR" -X POST "$BASE_URL/api/projects" \
    -H "Content-Type: application/json" \
    -d '{"name":"Security Probe Test Project (safe to delete)","description":"Created by scripts/security-probe.sh; deleted automatically when the probe finishes."}')"
# Anchored to the start of the object, not a bare leading `.*` — the response also contains a
# nested "owner":{"id":"..."}...} and a greedy leading `.*` would match THAT "id" instead (sed's
# BRE `.*` is greedy, so it skips past the first "id" to find the last one on the line), silently
# extracting the admin's own user ID instead of the project ID. Caught by this script's own
# cleanup failing to actually delete the throwaway project during verification.
PROJECT_ID="$(printf '%s' "$create_response" | sed -n 's/^{"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
if [ -z "$PROJECT_ID" ]; then
    st_error "[FAIL] Could not create the throwaway probe project — got: $create_response"
    FAIL=$((FAIL + 1))
else
    status="$(curl -s -b "$MEMBER_JAR" -o /dev/null -w '%{http_code}' "$BASE_URL/api/projects/$PROJECT_ID")"
    if [ "$status" = "403" ] || [ "$status" = "404" ]; then
        st_info "[PASS] GET /api/projects/{id} as a non-member -> $status (never 200)"
        PASS=$((PASS + 1))
    else
        st_error "[FAIL] GET /api/projects/{id} as a non-member -> $status (expected 403 or 404, never 200)"
        FAIL=$((FAIL + 1))
    fi
fi

st_step "Probe: security response headers present"
headers="$(curl -fsSI --max-time 5 "$BASE_URL/api/health" 2>/dev/null || true)"
if printf '%s' "$headers" | grep -qi "^Content-Security-Policy:" && \
   printf '%s' "$headers" | grep -qi "^X-Content-Type-Options: *nosniff" && \
   printf '%s' "$headers" | grep -qi "^X-Frame-Options: *DENY"; then
    st_info "[PASS] Content-Security-Policy/X-Content-Type-Options/X-Frame-Options all present"
    PASS=$((PASS + 1))
else
    st_error "[FAIL] one or more security response headers missing"
    FAIL=$((FAIL + 1))
fi

echo ""
echo "=================================================="
echo "Passed: $PASS  Failed: $FAIL"
echo "=================================================="
[ "$FAIL" -eq 0 ]
