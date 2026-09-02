#!/bin/bash
# Silver Task — dependency vulnerability check (Phase 59).
#
# Dev/CI-oriented, not part of the production --doctor/--security-check checks in
# update-debian.sh — a hardened production host may deliberately block egress to the NuGet/npm
# registries this needs to reach, and a vulnerability scan has no business running on every
# deploy anyway. Run this locally or in CI before tagging a release, alongside
# scripts/certify-release.sh (Phase 57).
#
# Usage: bash scripts/check-dependencies.sh
#
# Exit codes: 0 clean, 1 vulnerabilities found, 2 a scan itself could not run (network/tooling
# problem — distinct from "found something," since it means this check couldn't actually verify
# anything either way).

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

FAIL=0

st_step "Checking .NET dependencies (dotnet list package --vulnerable)"
if ! command -v dotnet >/dev/null 2>&1; then
    st_error "dotnet not found on PATH."
    FAIL=1
else
    DOTNET_OUTPUT="$(dotnet list "$REPO_ROOT/Silver-Task.Server" package --vulnerable --include-transitive 2>&1)"
    DOTNET_RC=$?
    echo "$DOTNET_OUTPUT"
    if [ "$DOTNET_RC" -ne 0 ]; then
        st_error "dotnet list package --vulnerable could not run (network/tooling problem — see output above)."
        FAIL=2
    elif printf '%s' "$DOTNET_OUTPUT" | grep -qi "has the following vulnerable packages"; then
        st_error "Vulnerable .NET package(s) found — see above. Run 'dotnet list package --vulnerable --include-transitive' locally for the full detail, then upgrade the affected package."
        [ "$FAIL" -eq 0 ] && FAIL=1
    else
        st_info "No known vulnerable .NET packages."
    fi
fi

echo ""
st_step "Checking npm dependencies (npm audit)"
if ! command -v npm >/dev/null 2>&1; then
    st_error "npm not found on PATH."
    FAIL=1
else
    NPM_OUTPUT="$(cd "$REPO_ROOT/silver-task.client" && npm audit 2>&1)"
    NPM_RC=$?
    echo "$NPM_OUTPUT"
    if printf '%s' "$NPM_OUTPUT" | grep -qi "found 0 vulnerabilities"; then
        st_info "No known vulnerable npm packages."
    elif [ "$NPM_RC" -ne 0 ]; then
        st_error "Vulnerable npm package(s) found (or 'npm audit' could not complete) — see above. Run 'npm audit' locally in silver-task.client/ for the full detail and 'npm audit fix' options."
        [ "$FAIL" -eq 0 ] && FAIL=1
    fi
fi

echo ""
if [ "$FAIL" -eq 0 ]; then
    st_info "=================================================================="
    st_info " DEPENDENCY CHECK CLEAN — no known vulnerabilities found"
    st_info "=================================================================="
else
    st_error "=================================================================="
    st_error " DEPENDENCY CHECK FOUND ISSUES — see output above"
    st_error "=================================================================="
fi
exit "$FAIL"
