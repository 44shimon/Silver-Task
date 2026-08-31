#!/bin/bash
# Silver Task — shared helpers for scripts/install-debian.sh, update-debian.sh,
# backup-debian.sh, and uninstall-debian.sh. Sourced, never executed directly — keeps the
# four operator-facing scripts from re-implementing the same logging/confirmation/secret-
# generation/service-detection code four different (and eventually inconsistent) ways.
#
# Every script that sources this must itself set `set -euo pipefail` — this file does not
# set it globally so it can be sourced safely from a context that's already configured its
# own shell options.

# --- Fixed locations, shared across every script (single source of truth) ---
: "${SILVERTASK_INSTALL_DIR:=/opt/silver-task}"
: "${SILVERTASK_SERVICE_USER:=silvertask}"
: "${SILVERTASK_SERVICE_NAME:=silvertask}"
: "${SILVERTASK_ENV_DIR:=/etc/silvertask}"
: "${SILVERTASK_ENV_FILE:=/etc/silvertask/silvertask.env}"
: "${SILVERTASK_BACKUP_DIR:=/var/backups/silver-task}"
: "${SILVERTASK_LOG_FILE:=/var/log/silver-task-install.log}"
: "${SILVERTASK_REPO_URL:=https://github.com/44shimon/Silver-Task.git}"
: "${SILVERTASK_PUBLISH_DIR:=$SILVERTASK_INSTALL_DIR/publish}"
: "${SILVERTASK_SOURCE_DIR:=$SILVERTASK_INSTALL_DIR/source}"

# --- Output / logging ---
# Every log line goes to both the terminal and SILVERTASK_LOG_FILE (when writable — scripts
# run read-only-safe commands, like --help, before root/log-file setup happens). Never pass
# a secret value to these — see each script's own "never log secrets" callouts at the call
# sites that handle generated passwords/JWT secrets.
_st_log_line() {
    local level="$1"; shift
    local line
    line="[$(date -u '+%Y-%m-%dT%H:%M:%SZ')] [$level] $*"
    echo "$line"
    if [ -w "$(dirname "$SILVERTASK_LOG_FILE")" ] || [ -w "$SILVERTASK_LOG_FILE" ] 2>/dev/null; then
        echo "$line" >> "$SILVERTASK_LOG_FILE" 2>/dev/null || true
    fi
}
st_info()  { _st_log_line "INFO" "$*"; }
st_warn()  { _st_log_line "WARN" "$*" >&2; }
st_error() { _st_log_line "ERROR" "$*" >&2; }
st_step()  { _st_log_line "STEP" "== $* =="; }

# Prints the failed step, a diagnostic hint if given, and exits non-zero. Every script uses
# this instead of letting `set -e` exit silently, per the spec's own "display the failed
# step, display useful diagnostic information, return a non-zero exit code" requirement.
st_fail() {
    local message="$1"
    local hint="${2:-}"
    st_error "$message"
    [ -n "$hint" ] && st_error "  Hint: $hint"
    st_error "See $SILVERTASK_LOG_FILE for the full log."
    exit 1
}

# Runs a command as the `postgres` OS user. Local PostgreSQL connections use peer
# authentication (Debian's default cluster config), which requires actually switching to that
# OS user, not just supplying app-level DB credentials. Uses `runuser`, not `sudo`: every
# script that calls this already runs as root itself (st_require_root), and `sudo` is an
# optional package that isn't guaranteed to be present on a fresh/minimal Debian install —
# found for real on a fresh Debian 13 test server that had no sudo binary at all. `runuser` is
# part of util-linux, which is `Essential: yes` on Debian and always present.
st_run_as_postgres() {
    runuser -u postgres -- "$@"
}

st_require_root() {
    if [ "$(id -u)" -ne 0 ]; then
        st_fail "This script must be run as root (or via sudo)." "sudo $0 $*"
    fi
}

# --- OS / environment checks ---
st_check_debian() {
    if [ ! -r /etc/os-release ]; then
        st_fail "Cannot read /etc/os-release — this doesn't look like a Debian-based system."
    fi
    # shellcheck disable=SC1091
    . /etc/os-release
    if [ "${ID:-}" != "debian" ]; then
        st_fail "This installer targets Debian (detected: ${PRETTY_NAME:-${ID:-unknown}})." \
            "Run on a fresh Debian 12 (bookworm) or newer server, or adapt the script for your distribution."
    fi
    local major_version="${VERSION_ID%%.*}"
    if [ -n "${major_version:-}" ] && [ "$major_version" -lt 12 ] 2>/dev/null; then
        st_warn "Detected Debian $VERSION_ID — this installer is tested against Debian 12 (bookworm) and newer. Continuing, but package availability (.NET, Node.js) may differ on older releases."
    fi
    st_info "OS check passed: ${PRETTY_NAME:-Debian $VERSION_ID}"
}

st_check_arch() {
    local arch
    arch="$(dpkg --print-architecture)"
    case "$arch" in
        amd64|arm64) st_info "Architecture check passed: $arch" ;;
        *) st_fail "Unsupported architecture: $arch (Silver Task's .NET runtime supports amd64/arm64)." ;;
    esac
}

st_check_resources() {
    local min_disk_mb=2048 min_ram_mb=1024
    local avail_disk_mb avail_ram_mb
    avail_disk_mb=$(df -Pm "$(dirname "$SILVERTASK_INSTALL_DIR")" 2>/dev/null | awk 'NR==2 {print $4}')
    avail_ram_mb=$(awk '/MemTotal/ {print int($2/1024)}' /proc/meminfo)

    if [ -n "${avail_disk_mb:-}" ] && [ "$avail_disk_mb" -lt "$min_disk_mb" ]; then
        st_fail "Only ${avail_disk_mb}MB free disk space (need at least ${min_disk_mb}MB)." "Free up disk space and re-run."
    fi
    st_info "Disk space check passed: ${avail_disk_mb:-unknown}MB available"

    if [ -n "${avail_ram_mb:-}" ] && [ "$avail_ram_mb" -lt "$min_ram_mb" ]; then
        st_warn "Only ${avail_ram_mb}MB RAM detected (recommended at least ${min_ram_mb}MB). The .NET build step in particular may be slow or swap heavily."
    else
        st_info "RAM check passed: ${avail_ram_mb:-unknown}MB"
    fi
}

st_check_internet() {
    # Deliberately NOT using curl here — this check runs during pre-flight, before the
    # "install system dependencies" step that's what actually installs curl. A minimal/fresh
    # Debian install may not have curl (or wget) pre-installed at all, which would make a
    # curl-based check fail with "command not found" and get misreported as "no internet"
    # (found via a real Debian 13 test run — see the script's own change history). Bash's
    # /dev/tcp pseudo-device needs nothing beyond bash itself: it opens a raw TCP connection,
    # which is exactly "is there a route to the internet" without depending on any tool this
    # script hasn't installed yet.
    if ! timeout 5 bash -c 'exec 3<>/dev/tcp/deb.debian.org/443' 2>/dev/null; then
        st_fail "No internet connectivity (could not open a TCP connection to deb.debian.org:443)." \
            "Check network/DNS/firewall configuration. If deb.debian.org is reachable but this still fails, your kernel may have /dev/tcp disabled — check 'bash -c \"echo > /dev/tcp/deb.debian.org/443\"' manually."
    fi
    st_info "Internet connectivity check passed."
}

st_check_ports() {
    local occupied=()
    local port holders
    for port in "$@"; do
        holders="$(ss -ltnp "( sport = :$port )" 2>/dev/null | tail -n +2)"
        [ -z "$holders" ] && continue
        # A port held only by nginx is not a real conflict: nginx is a service this
        # installer owns outright and (re)configures every run (including re-runs after an
        # earlier attempt failed partway through and left nginx running with the default
        # site on this port, or a previous successful install that legitimately has nginx
        # bound here already). Only flag it if something else is holding the port.
        if echo "$holders" | grep -qv '"nginx"'; then
            occupied+=("$port")
        else
            st_info "Port $port is already in use by nginx only (from an earlier install run, or nginx's default config) — this installer reconfigures nginx itself, so this is not a conflict."
        fi
    done
    if [ "${#occupied[@]}" -gt 0 ]; then
        st_error "Required port(s) already in use: ${occupied[*]}"
        for port in "${occupied[@]}"; do
            st_error "  Port $port is held by:"
            ss -ltnp "( sport = :$port )" 2>/dev/null | tail -n +2 | while IFS= read -r line; do st_error "    $line"; done
        done
        st_fail "Free the port(s) above, or re-run with a different --http-port/--https-port." \
            "e.g. sudo ./scripts/install-debian.sh --http-port=8080 --https-port=8443"
    fi
    st_info "Port availability check passed: ${*}"
}

# --- Secrets ---
# Hex output only (no base64) so generated values are always safe to embed directly in a
# Npgsql connection string (`;`/`=` in a base64 password would break key=value parsing) and
# never need additional quoting in the systemd EnvironmentFile format.
st_generate_secret() {
    local bytes="${1:-32}"
    openssl rand -hex "$bytes"
}

# --- Installation detection (idempotency — spec's own "running the installer twice should
# not destroy an existing installation" requirement) ---
st_is_installed() {
    [ -d "$SILVERTASK_INSTALL_DIR" ] && [ -f "$SILVERTASK_ENV_FILE" ] && [ -f "/etc/systemd/system/${SILVERTASK_SERVICE_NAME}.service" ]
}

# --- Confirmation prompt for destructive actions. Always requires an exact match, never a
# bare Enter/Y shortcut, for anything that can delete data. ---
st_confirm_destructive() {
    local prompt="$1"
    local expected="$2"
    local reply
    read -r -p "$prompt Type '$expected' to confirm, anything else to cancel: " reply
    [ "$reply" = "$expected" ]
}

# --- Health check against the app's own liveness/readiness endpoints (Phase 48) ---
st_health_check() {
    local base_url="$1"
    local attempts="${2:-10}"
    local delay="${3:-3}"
    local i
    for ((i = 1; i <= attempts; i++)); do
        if curl -fsS --max-time 5 "$base_url/api/health/ready" >/dev/null 2>&1; then
            st_info "Health check passed ($base_url/api/health/ready)."
            return 0
        fi
        st_info "Health check attempt $i/$attempts not ready yet, retrying in ${delay}s..."
        sleep "$delay"
    done
    return 1
}
