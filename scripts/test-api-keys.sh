#!/bin/bash
# Silver Task — live API key & service account authentication probe (Phase 62).
#
# Same "live orchestration against a real app" category as security-probe.sh, not the pure
# portable-logic test-*.sh naming (test-upgrade-engine.sh/test-certify-release.sh) — this
# genuinely requires a running instance with the seeded demo Administrator.
#
# Creates one throwaway service account + API key via the new admin endpoints
# (POST /api/admin/service-accounts, POST /api/admin/api-keys), exercises the ApiKey
# authentication scheme against Controllers/V1/*, then cleans up (deactivates the service
# account, which also revokes its keys — see ApiKeyService.DeactivateServiceAccountAsync)
# regardless of pass/fail, mirroring security-probe.sh's own IDOR-probe cleanup discipline.
#
# Usage:
#   bash scripts/test-api-keys.sh
#   bash scripts/test-api-keys.sh --base-url=https://staging.example.com \
#       --admin-email=admin@example.com --admin-password='...'
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

st_apikeytest_usage() {
    cat <<'EOF'
Usage: bash scripts/test-api-keys.sh [options]

  --base-url=URL           Default: http://127.0.0.1:5000
  --admin-email=EMAIL      Default: admin@example.com (DemoDataSeeder's seeded Administrator)
  --admin-password=PASS    Default: Demo1234!
  --help, -h                Show this message.
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --base-url=*) BASE_URL="${1#*=}" ;;
        --admin-email=*) ADMIN_EMAIL="${1#*=}" ;;
        --admin-password=*) ADMIN_PASSWORD="${1#*=}" ;;
        --help|-h) st_apikeytest_usage; exit 0 ;;
        *) st_apikeytest_usage >&2; echo "ERROR: Unknown argument: $1" >&2; exit 2 ;;
    esac
    shift
done

PASS=0
FAIL=0
SERVICE_ACCOUNT_ID=""
ADMIN_JAR="$(mktemp)"

cleanup() {
    if [ -n "$SERVICE_ACCOUNT_ID" ]; then
        curl -fsS -b "$ADMIN_JAR" -X DELETE "$BASE_URL/api/admin/service-accounts/$SERVICE_ACCOUNT_ID" >/dev/null 2>&1 || true
    fi
    rm -f "$ADMIN_JAR"
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

echo "Silver Task API Key & Service Account Authentication Test"
echo "Base URL: $BASE_URL"
echo ""

st_step "Logging in as admin"
admin_login_status="$(curl -fsS -c "$ADMIN_JAR" -o /dev/null -w '%{http_code}' -X POST "$BASE_URL/api/auth/login" \
    -H "Content-Type: application/json" -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}")"
if [ "$admin_login_status" != "200" ]; then
    st_error "Could not log in as admin ($ADMIN_EMAIL) — got HTTP $admin_login_status. Nothing else can be tested."
    exit 2
fi
st_info "Logged in as admin."
echo ""

st_step "Creating a throwaway service account + API key"
sa_response="$(curl -fsS -b "$ADMIN_JAR" -X POST "$BASE_URL/api/admin/service-accounts" \
    -H "Content-Type: application/json" \
    -d '{"name":"test-api-keys.sh probe account (safe to delete)","role":"Member"}')"
SERVICE_ACCOUNT_ID="$(printf '%s' "$sa_response" | sed -n 's/^{"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
if [ -z "$SERVICE_ACCOUNT_ID" ]; then
    st_error "Could not create the throwaway service account — got: $sa_response"
    exit 2
fi
st_info "Created service account $SERVICE_ACCOUNT_ID."

key_response="$(curl -fsS -b "$ADMIN_JAR" -X POST "$BASE_URL/api/admin/api-keys" \
    -H "Content-Type: application/json" \
    -d "{\"userId\":\"$SERVICE_ACCOUNT_ID\",\"name\":\"test-api-keys.sh probe key\"}")"
# "key" is the first field ApiKeyCreatedDto serializes (it's declared on the derived class),
# "id" the second — both anchored at the start of the object, matched together in one pattern.
# A bare, unanchored `.*"id":"..."` would be the exact greedy-.* trap security-probe.sh's own
# PROJECT_ID extraction already documents: this response also nests "owner":{"id":...} and
# "createdBy":{"id":...}, and sed's BRE `.*` is greedy — it skips past the key's own id to match
# the LAST "id" on the line (createdBy's) instead. Caught here the same way that one was: by
# rotation subsequently 404ing on the wrong id.
API_KEY="$(printf '%s' "$key_response" | sed -n 's/^{"key"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
KEY_ID="$(printf '%s' "$key_response" | sed -n 's/^{"key"[[:space:]]*:[[:space:]]*"[^"]*"[[:space:]]*,[[:space:]]*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
if [ -z "$API_KEY" ] || [ -z "$KEY_ID" ]; then
    st_error "Could not create the throwaway API key — got: $key_response"
    exit 2
fi
st_info "Created API key $KEY_ID."
echo ""

st_step "Probe: valid API key authenticates against a v1 endpoint"
valid_key_body="$(curl -s -H "X-Api-Key: $API_KEY" -w '\n%{http_code}' "$BASE_URL/api/v1/projects")"
status="$(printf '%s' "$valid_key_body" | tail -1)"
valid_key_body="$(printf '%s' "$valid_key_body" | sed '$d')"
assert_status "GET /api/v1/projects with a valid X-Api-Key -> 200" "200" "$status"

st_step "Probe: a service account scoped to zero project memberships gets an empty list, not 403"
# Same authorization behavior a human member with no project memberships already gets —
# IProjectService.GetAllForUserAsync filters to what the caller belongs to, it never 403s for
# "belongs to nothing." The freshly-created service account above was never added to any
# project's Members, so this should be an empty page, not a rejection.
if printf '%s' "$valid_key_body" | grep -q '"totalCount":0'; then
    st_info "[PASS] Zero-membership service account gets an empty (not forbidden) project list"
    PASS=$((PASS + 1))
else
    st_error "[FAIL] Expected an empty totalCount:0 page for a zero-membership service account — got: $valid_key_body"
    FAIL=$((FAIL + 1))
fi

st_step "Probe: cookie-only session still works on v1 endpoints (ApiKey scheme is additive)"
status="$(curl -s -b "$ADMIN_JAR" -o /dev/null -w '%{http_code}' "$BASE_URL/api/v1/projects")"
assert_status "GET /api/v1/projects with only a cookie session -> 200" "200" "$status"

st_step "Probe: no key and no cookie is rejected"
status="$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/api/v1/projects")"
assert_status "GET /api/v1/projects with no auth at all -> 401" "401" "$status"

st_step "Probe: garbage key is rejected"
status="$(curl -s -H "X-Api-Key: not-a-real-key" -o /dev/null -w '%{http_code}' "$BASE_URL/api/v1/projects")"
assert_status "GET /api/v1/projects with a garbage X-Api-Key -> 401" "401" "$status"

st_step "Probe: a service account can never authenticate via password login"
sa_email="$(printf '%s' "$sa_response" | sed -n 's/.*"email"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
status="$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE_URL/api/auth/login" \
    -H "Content-Type: application/json" -d "{\"email\":\"$sa_email\",\"password\":\"anything\"}")"
assert_status "POST /api/auth/login as a service account -> 401 (regardless of password)" "401" "$status"

st_step "Probe: rotation invalidates the old key and the new one works"
rotate_response="$(curl -fsS -b "$ADMIN_JAR" -X POST "$BASE_URL/api/admin/api-keys/$KEY_ID/rotate")"
NEW_KEY="$(printf '%s' "$rotate_response" | sed -n 's/^{"key"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
NEW_KEY_ID="$(printf '%s' "$rotate_response" | sed -n 's/^{"key"[[:space:]]*:[[:space:]]*"[^"]*"[[:space:]]*,[[:space:]]*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
if [ -z "$NEW_KEY" ]; then
    st_error "[FAIL] Rotation did not return a new key — got: $rotate_response"
    FAIL=$((FAIL + 1))
else
    status="$(curl -s -H "X-Api-Key: $API_KEY" -o /dev/null -w '%{http_code}' "$BASE_URL/api/v1/projects")"
    assert_status "Old key rejected after rotation" "401" "$status"
    status="$(curl -s -H "X-Api-Key: $NEW_KEY" -o /dev/null -w '%{http_code}' "$BASE_URL/api/v1/projects")"
    assert_status "New (rotated) key works" "200" "$status"
fi

st_step "Probe: revoked key is rejected"
if [ -n "$NEW_KEY_ID" ]; then
    curl -fsS -b "$ADMIN_JAR" -X DELETE "$BASE_URL/api/admin/api-keys/$NEW_KEY_ID" >/dev/null 2>&1
    status="$(curl -s -H "X-Api-Key: $NEW_KEY" -o /dev/null -w '%{http_code}' "$BASE_URL/api/v1/projects")"
    assert_status "GET /api/v1/projects with a revoked X-Api-Key -> 401" "401" "$status"
fi

st_step "Probe: repeated invalid attempts trip the failure tracker (further attempts still 401, fast)"
threshold_hit=false
for _ in $(seq 1 12); do
    status="$(curl -s -H "X-Api-Key: scan-attempt-$RANDOM" -o /dev/null -w '%{http_code}' "$BASE_URL/api/v1/projects")"
    if [ "$status" != "401" ]; then
        threshold_hit=true
        break
    fi
done
if [ "$threshold_hit" = false ]; then
    st_info "[PASS] 12 consecutive invalid X-Api-Key attempts all rejected with 401 (never a 5xx or unexpected status)"
    PASS=$((PASS + 1))
else
    st_error "[FAIL] An invalid API key attempt returned something other than 401"
    FAIL=$((FAIL + 1))
fi

echo ""
echo "=================================================="
echo "Passed: $PASS  Failed: $FAIL"
echo "=================================================="
[ "$FAIL" -eq 0 ]
