#!/bin/bash
# Silver Task — Debian installer.
#
# Installs Silver Task (ASP.NET Core backend + bundled React SPA + PostgreSQL) on a fresh
# Debian 12+ server: .NET SDK, Node.js (needed to build the SPA), PostgreSQL, nginx as a
# TLS-terminating reverse proxy, and a systemd service — reusing exactly the architecture
# already documented in DEPLOYMENT.md and deploy/ (this script is what actually executes
# those steps instead of a human following them by hand). No Docker is used because this
# application has no Docker/Compose configuration anywhere in the repository (see Phase 50's
# own "do not assume technologies that are not present" instruction).
#
# Usage:
#   sudo ./scripts/install-debian.sh                    # interactive
#   sudo ./scripts/install-debian.sh --non-interactive   # automated, see flags/env vars below
#
# Run from inside a cloned copy of the repository (see README "Quick Start") — the script
# installs the checkout it's run from, it does not re-clone from GitHub itself.
#
# Non-interactive flags (all optional; sensible defaults used when omitted):
#   --domain=tasks.example.com     Public domain. Omit for IP-only access (no HTTPS/certbot).
#   --http-port=80                 Public HTTP port (redirects to HTTPS if a domain is set).
#   --https-port=443               Public HTTPS port (ignored if no domain — see --domain).
#   --admin-email=you@example.com  Creates the Administrator account automatically once the
#                                   app is up, with a randomly generated password printed at
#                                   the end of installation. Omit to skip and create it
#                                   yourself later (see README "Quick Start").
#   --admin-name=...               Display name for the account above (default: Administrator).
#   --smtp-host=... --smtp-port=587 --smtp-username=... --smtp-password=... --smtp-from=...
#                                   Optional. Silver Task runs fully functional without email.
#   --skip-ssl                     Configure nginx for HTTP only, even if --domain is set
#                                   (e.g. TLS terminated further upstream already).
#   --skip-firewall                Do not touch ufw.
#   --non-interactive              Do not prompt; use flags/env vars/defaults only.
#
# Equivalent environment variables (useful for CI/config-management, e.g. Ansible):
#   SILVERTASK_DOMAIN, SILVERTASK_HTTP_PORT, SILVERTASK_HTTPS_PORT, SILVERTASK_SKIP_SSL,
#   SILVERTASK_ADMIN_EMAIL, SILVERTASK_ADMIN_NAME,
#   SILVERTASK_SMTP_HOST, SILVERTASK_SMTP_PORT, SILVERTASK_SMTP_USERNAME,
#   SILVERTASK_SMTP_PASSWORD, SILVERTASK_SMTP_FROM

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

NON_INTERACTIVE=false
SKIP_FIREWALL=false
SKIP_SSL="${SILVERTASK_SKIP_SSL:-false}"
DOMAIN="${SILVERTASK_DOMAIN:-}"
HTTP_PORT="${SILVERTASK_HTTP_PORT:-80}"
HTTPS_PORT="${SILVERTASK_HTTPS_PORT:-443}"
ADMIN_EMAIL="${SILVERTASK_ADMIN_EMAIL:-}"
ADMIN_NAME="${SILVERTASK_ADMIN_NAME:-Administrator}"
SMTP_HOST="${SILVERTASK_SMTP_HOST:-}"
SMTP_PORT="${SILVERTASK_SMTP_PORT:-587}"
SMTP_USERNAME="${SILVERTASK_SMTP_USERNAME:-}"
SMTP_PASSWORD="${SILVERTASK_SMTP_PASSWORD:-}"
SMTP_FROM="${SILVERTASK_SMTP_FROM:-}"
STORAGE_ROOT="/var/lib/silver-task/attachments"

print_help() {
    sed -n '2,39p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

for arg in "$@"; do
    case "$arg" in
        --help|-h) print_help; exit 0 ;;
        --non-interactive) NON_INTERACTIVE=true ;;
        --skip-firewall) SKIP_FIREWALL=true ;;
        --skip-ssl) SKIP_SSL=true ;;
        --domain=*) DOMAIN="${arg#*=}" ;;
        --http-port=*) HTTP_PORT="${arg#*=}" ;;
        --https-port=*) HTTPS_PORT="${arg#*=}" ;;
        --admin-email=*) ADMIN_EMAIL="${arg#*=}" ;;
        --admin-name=*) ADMIN_NAME="${arg#*=}" ;;
        --smtp-host=*) SMTP_HOST="${arg#*=}" ;;
        --smtp-port=*) SMTP_PORT="${arg#*=}" ;;
        --smtp-username=*) SMTP_USERNAME="${arg#*=}" ;;
        --smtp-password=*) SMTP_PASSWORD="${arg#*=}" ;;
        --smtp-from=*) SMTP_FROM="${arg#*=}" ;;
        *) st_fail "Unknown argument: $arg" "Run with --help for usage." ;;
    esac
done

trap 'st_error "Installation aborted (line $LINENO). See $SILVERTASK_LOG_FILE for details."' ERR

st_require_root "$@"
mkdir -p "$(dirname "$SILVERTASK_LOG_FILE")"
touch "$SILVERTASK_LOG_FILE" && chmod 640 "$SILVERTASK_LOG_FILE"

st_step "Pre-flight checks"
st_check_debian
st_check_arch
st_check_resources
st_check_internet

if st_is_installed; then
    st_warn "Silver Task already appears to be installed at $SILVERTASK_INSTALL_DIR."
    st_warn "Re-running install will NOT delete your database, uploaded files, or existing $SILVERTASK_ENV_FILE."
    st_warn "To apply a new version, use scripts/update-debian.sh instead — it backs up before changing anything."
    if [ "$NON_INTERACTIVE" = false ]; then
        read -r -p "Continue anyway and re-apply configuration/build? [y/N] " reply
        [ "$reply" = "y" ] || [ "$reply" = "Y" ] || { st_info "Aborted by user."; exit 0; }
    else
        st_info "Non-interactive mode: continuing to re-apply configuration (existing .env/database preserved)."
    fi
fi

# --- Interactive prompts (skipped entirely in --non-interactive mode) ---
if [ "$NON_INTERACTIVE" = false ]; then
    echo
    echo "Silver Task — installation configuration"
    echo "Press Enter to accept the default shown in [brackets]."
    echo
    read -r -p "Domain name (leave blank for IP-only access, no HTTPS): [${DOMAIN}] " input
    DOMAIN="${input:-$DOMAIN}"
    if [ -n "$DOMAIN" ]; then
        read -r -p "HTTP port [$HTTP_PORT]: " input; HTTP_PORT="${input:-$HTTP_PORT}"
        read -r -p "HTTPS port [$HTTPS_PORT]: " input; HTTPS_PORT="${input:-$HTTPS_PORT}"
        if [ "$SKIP_SSL" = false ]; then
            read -r -p "Request a Let's Encrypt certificate for $DOMAIN now via certbot? [Y/n] " input
            [ "$input" = "n" ] || [ "$input" = "N" ] && SKIP_SSL=true
        fi
    else
        read -r -p "HTTP port for local/IP access [$HTTP_PORT]: " input; HTTP_PORT="${input:-$HTTP_PORT}"
        SKIP_SSL=true
        st_info "No domain given — installing for HTTP/IP access only. You can re-run later with --domain to add HTTPS."
    fi
    echo
    echo "An administrator account can be created automatically once installation finishes —"
    echo "its password is randomly generated and shown once at the end. Leave blank to skip"
    echo "and create one yourself later (see README \"Quick Start\")."
    read -r -p "Administrator email: [${ADMIN_EMAIL}] " input
    ADMIN_EMAIL="${input:-$ADMIN_EMAIL}"
    if [ -n "$ADMIN_EMAIL" ]; then
        read -r -p "Administrator name [${ADMIN_NAME}]: " input
        ADMIN_NAME="${input:-$ADMIN_NAME}"
    fi
    echo
    echo "Email (SMTP) is optional — Silver Task works fully without it (email notifications simply stay off)."
    read -r -p "SMTP host (leave blank to skip): [${SMTP_HOST}] " input
    SMTP_HOST="${input:-$SMTP_HOST}"
    if [ -n "$SMTP_HOST" ]; then
        read -r -p "SMTP port [$SMTP_PORT]: " input; SMTP_PORT="${input:-$SMTP_PORT}"
        read -r -p "SMTP username: [${SMTP_USERNAME}] " input; SMTP_USERNAME="${input:-$SMTP_USERNAME}"
        read -r -s -p "SMTP password: " SMTP_PASSWORD; echo
        read -r -p "From address [${SMTP_FROM:-$SMTP_USERNAME}]: " input; SMTP_FROM="${input:-${SMTP_FROM:-$SMTP_USERNAME}}"
    fi
    echo
fi

PUBLIC_PORT_FOR_HEALTHCHECK="$HTTP_PORT"
[ -n "$DOMAIN" ] && [ "$SKIP_SSL" = false ] && PUBLIC_PORT_FOR_HEALTHCHECK="$HTTPS_PORT"

REQUIRED_PORTS=("$HTTP_PORT")
[ -n "$DOMAIN" ] && [ "$SKIP_SSL" = false ] && REQUIRED_PORTS+=("$HTTPS_PORT")
st_step "Checking required ports are free"
st_check_ports "${REQUIRED_PORTS[@]}"

# --- System packages ---
st_step "Installing system dependencies"
export DEBIAN_FRONTEND=noninteractive

# Defensive cleanup: if packages.microsoft.com's apt repo was ever registered on this host —
# by an older version of this script (before .NET/Node.js moved to direct vendor downloads,
# see below) or by a human following Microsoft's own install docs — it now breaks apt-get
# update outright, not just Microsoft package installs: Debian 13 (trixie)'s tightened
# apt/sequoia signature policy rejects that repo's SHA1-signed key unconditionally, so every
# apt-get update on the host fails while the source file exists. Found for real: a prior
# partial run on the test server left it registered, and it silently broke this step even
# though this step doesn't need anything from that repo.
if [ -f /etc/apt/sources.list.d/microsoft-prod.list ]; then
    st_warn "Removing a leftover packages.microsoft.com apt source — incompatible with Debian 13's signature policy and no longer used by this installer (.NET/Node.js are installed via direct downloads instead)."
    rm -f /etc/apt/sources.list.d/microsoft-prod.list
    rm -f /usr/share/keyrings/microsoft-prod.gpg /etc/apt/trusted.gpg.d/microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.asc
fi

apt-get update -qq || st_fail "apt-get update failed."
apt-get install -y -qq \
    curl ca-certificates gnupg apt-transport-https lsb-release openssl \
    postgresql postgresql-contrib nginx ufw rsync \
    >> "$SILVERTASK_LOG_FILE" 2>&1 \
    || st_fail "Installing base packages failed." "Check $SILVERTASK_LOG_FILE for apt's output."
st_info "Base packages installed."

# Both .NET and Node.js are installed via their vendors' own direct HTTPS binary downloads,
# NOT via packages.microsoft.com/NodeSource's apt repos. Found the hard way: Microsoft's repo
# signing key uses an older SHA1-based certification signature that Debian 13 (trixie)'s
# tightened apt/sequoia cryptographic policy rejects outright ("SHA1 is not considered secure
# since 2026-02-01") — not a wrong-Debian-version-config problem, a hard policy rejection with
# no apt-side workaround short of lowering the whole system's crypto policy (not something an
# installer should ever do). Downloading the official tarball/install script directly sidesteps
# third-party apt-repo trust chains entirely and works identically across Debian versions.
DOTNET_ARCH="x64"; [ "$(dpkg --print-architecture)" = "arm64" ] && DOTNET_ARCH="arm64"
if ! command -v dotnet >/dev/null 2>&1; then
    st_step "Installing .NET SDK 10"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
        || st_fail "Could not download dotnet-install.sh." "Check https://dot.net/v1/dotnet-install.sh is reachable."
    bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet --architecture "$DOTNET_ARCH" >> "$SILVERTASK_LOG_FILE" 2>&1 \
        || st_fail "Installing .NET SDK 10 failed." "Check $SILVERTASK_LOG_FILE."
    rm -f /tmp/dotnet-install.sh
    ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
    st_info "$(dotnet --version) SDK installed to /usr/share/dotnet."
else
    st_info ".NET SDK already present: $(dotnet --version)"
fi

NODE_ARCH="x64"; [ "$(dpkg --print-architecture)" = "arm64" ] && NODE_ARCH="arm64"
if ! command -v node >/dev/null 2>&1; then
    st_step "Installing Node.js (required to build the frontend)"
    NODE_TARBALL="$(curl -fsSL "https://nodejs.org/dist/latest-v22.x/" | grep -oP "node-v22\.[0-9.]+-linux-${NODE_ARCH}\.tar\.xz" | head -1)"
    [ -z "$NODE_TARBALL" ] && st_fail "Could not determine the latest Node.js 22.x download filename." "Check https://nodejs.org/dist/latest-v22.x/ manually and adjust the script if their listing format changed."
    curl -fsSL "https://nodejs.org/dist/latest-v22.x/$NODE_TARBALL" -o /tmp/node.tar.xz \
        || st_fail "Node.js download failed."
    mkdir -p /opt/nodejs
    tar -xJf /tmp/node.tar.xz -C /opt/nodejs --strip-components=1 \
        || st_fail "Extracting the Node.js archive failed."
    rm -f /tmp/node.tar.xz
    ln -sf /opt/nodejs/bin/node /usr/local/bin/node
    ln -sf /opt/nodejs/bin/npm /usr/local/bin/npm
    ln -sf /opt/nodejs/bin/npx /usr/local/bin/npx
    st_info "Node $(node --version) installed to /opt/nodejs."
else
    st_info "Node.js already present: $(node --version)"
fi

systemctl enable --now postgresql >> "$SILVERTASK_LOG_FILE" 2>&1
systemctl enable --now nginx >> "$SILVERTASK_LOG_FILE" 2>&1

# --- Service account ---
if ! id "$SILVERTASK_SERVICE_USER" >/dev/null 2>&1; then
    st_step "Creating service account '$SILVERTASK_SERVICE_USER'"
    useradd --system --home-dir "$SILVERTASK_INSTALL_DIR" --shell /usr/sbin/nologin "$SILVERTASK_SERVICE_USER"
fi

# --- Copy source into place (this script installs the checkout it's run from — see README
# "Quick Start"; it never re-clones from GitHub, so what gets installed is exactly what you
# already have checked out, .git included so update-debian.sh can `git pull` it later). ---
st_step "Installing application source to $SILVERTASK_SOURCE_DIR"
mkdir -p "$SILVERTASK_INSTALL_DIR"
rsync -a --delete \
    --exclude 'bin/' --exclude 'obj/' --exclude 'node_modules/' --exclude 'dist/' \
    --exclude 'App_Data/' --exclude '.vs/' \
    "$REPO_ROOT/" "$SILVERTASK_SOURCE_DIR/" \
    || st_fail "Copying application source failed."
# Pre-authorize root (update-debian.sh always runs as root) to run git against this tree even
# after it's chowned to $SILVERTASK_SERVICE_USER below — see st_trust_git_dir in common.sh.
st_trust_git_dir "$SILVERTASK_SOURCE_DIR"

# --- File storage ---
st_step "Configuring file storage at $STORAGE_ROOT"
mkdir -p "$STORAGE_ROOT"
chown -R "$SILVERTASK_SERVICE_USER:$SILVERTASK_SERVICE_USER" "$STORAGE_ROOT"
chmod 750 "$STORAGE_ROOT"

# --- Database ---
st_step "Configuring PostgreSQL database"
DB_NAME="silvertask"
DB_USER="silvertask_app"
mkdir -p "$SILVERTASK_ENV_DIR"

if [ -f "$SILVERTASK_ENV_FILE" ]; then
    st_info "Existing $SILVERTASK_ENV_FILE found — reusing its database credentials rather than generating new ones."
    # shellcheck disable=SC1090
    DB_PASSWORD="$(grep -oP '(?<=Password=)[^;]*' "$SILVERTASK_ENV_FILE" | head -1 || true)"
fi
DB_PASSWORD="${DB_PASSWORD:-$(st_generate_secret 24)}"

if ! st_run_as_postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='$DB_USER'" | grep -q 1; then
    st_run_as_postgres psql -c "CREATE ROLE $DB_USER LOGIN PASSWORD '$DB_PASSWORD';" >> "$SILVERTASK_LOG_FILE" 2>&1
else
    st_run_as_postgres psql -c "ALTER ROLE $DB_USER WITH PASSWORD '$DB_PASSWORD';" >> "$SILVERTASK_LOG_FILE" 2>&1
fi
if ! st_run_as_postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='$DB_NAME'" | grep -q 1; then
    st_run_as_postgres psql -c "CREATE DATABASE $DB_NAME OWNER $DB_USER;" >> "$SILVERTASK_LOG_FILE" 2>&1
    st_info "Database '$DB_NAME' created."
else
    st_info "Database '$DB_NAME' already exists — not recreated (never drops an existing production database)."
fi

# --- Environment file ---
st_step "Writing $SILVERTASK_ENV_FILE"
if [ -f "$SILVERTASK_ENV_FILE" ]; then
    JWT_SECRET="$(grep -oP '(?<=Jwt__Secret=).*' "$SILVERTASK_ENV_FILE" || true)"
fi
JWT_SECRET="${JWT_SECRET:-$(st_generate_secret 48)}"

CORS_ORIGIN=""
if [ -n "$DOMAIN" ]; then
    [ "$SKIP_SSL" = true ] && CORS_ORIGIN="http://$DOMAIN" || CORS_ORIGIN="https://$DOMAIN"
fi
APP_BASE_URL="$CORS_ORIGIN"

cat > "$SILVERTASK_ENV_FILE" <<EOF
# Generated by scripts/install-debian.sh on $(date -u '+%Y-%m-%dT%H:%M:%SZ') — do not commit this file.
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD
Jwt__Secret=$JWT_SECRET
Cors__AllowedOrigins__0=$CORS_ORIGIN
Attachments__StorageRoot=$STORAGE_ROOT
General__ApplicationBaseUrl=$APP_BASE_URL
Smtp__Host=$SMTP_HOST
Smtp__Port=$SMTP_PORT
Smtp__EnableSsl=true
Smtp__Username=$SMTP_USERNAME
Smtp__Password=$SMTP_PASSWORD
Smtp__FromAddress=$SMTP_FROM
Smtp__FromName=Silver Task
EOF
chmod 600 "$SILVERTASK_ENV_FILE"
chown root:"$SILVERTASK_SERVICE_USER" "$SILVERTASK_ENV_FILE"
chmod 640 "$SILVERTASK_ENV_FILE"
st_info "Environment file written (permissions 640, owned by root:$SILVERTASK_SERVICE_USER). No secret values were logged."

# --- Build ---
st_step "Building Silver Task (dotnet publish -c Release) — this can take a few minutes"
(
    cd "$SILVERTASK_SOURCE_DIR"
    dotnet tool restore >> "$SILVERTASK_LOG_FILE" 2>&1
    dotnet publish Silver-Task.Server/Silver-Task.Server.csproj -c Release -o "$SILVERTASK_PUBLISH_DIR" >> "$SILVERTASK_LOG_FILE" 2>&1
) || st_fail "Build failed." "Check $SILVERTASK_LOG_FILE for the full dotnet publish/npm build output."
chown -R "$SILVERTASK_SERVICE_USER:$SILVERTASK_SERVICE_USER" "$SILVERTASK_INSTALL_DIR"
st_info "Build succeeded."

# Written to SILVERTASK_INSTALL_DIR (stable across future updates), not SILVERTASK_PUBLISH_DIR
# (replaced every update) — a durable, git-independent record of what's installed, readable even
# if the service is down. Distinct from GET /api/health's "version" field, which reports what's
# actually running right now. Best-effort git commit — install-debian.sh installs whatever
# checkout it's run from, which isn't guaranteed to be a tagged release the way
# update-debian.sh's --ref=<tag> flow is, so no hard tag-compatibility check here.
INSTALLED_VERSION="$(tr -d '[:space:]' < "$SILVERTASK_SOURCE_DIR/VERSION" 2>/dev/null || echo unknown)"
INSTALLED_COMMIT="$(git -C "$SILVERTASK_SOURCE_DIR" rev-parse --short HEAD 2>/dev/null || echo unknown)"
cat > "$SILVERTASK_INSTALL_DIR/installed-version.json" <<EOF
{
  "version": "$INSTALLED_VERSION",
  "gitCommit": "$INSTALLED_COMMIT",
  "installedAtUtc": "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
}
EOF
chown "$SILVERTASK_SERVICE_USER:$SILVERTASK_SERVICE_USER" "$SILVERTASK_INSTALL_DIR/installed-version.json"

# --- Migrations ---
st_step "Running database migrations"
(
    cd "$SILVERTASK_SOURCE_DIR"
    st_load_env_file "$SILVERTASK_ENV_FILE"
    dotnet ef database update --project Silver-Task.Server --startup-project Silver-Task.Server >> "$SILVERTASK_LOG_FILE" 2>&1
) || st_fail "Database migration failed." "Check $SILVERTASK_LOG_FILE. The application was NOT started with a partially-migrated database."
st_info "Migrations applied."

# --- systemd service ---
st_step "Installing systemd service"
DOTNET_BIN="$(command -v dotnet)" || st_fail "dotnet not found on PATH after installation." "This should not happen — check the .NET install step above in $SILVERTASK_LOG_FILE."
sed \
    -e "s#WorkingDirectory=.*#WorkingDirectory=$SILVERTASK_PUBLISH_DIR#" \
    -e "s#ExecStart=.*#ExecStart=$DOTNET_BIN $SILVERTASK_PUBLISH_DIR/Silver-Task.Server.dll#" \
    -e "s#User=.*#User=$SILVERTASK_SERVICE_USER#" \
    -e "s#EnvironmentFile=.*#EnvironmentFile=$SILVERTASK_ENV_FILE#" \
    "$REPO_ROOT/deploy/silvertask.service" > "/etc/systemd/system/${SILVERTASK_SERVICE_NAME}.service"
systemctl daemon-reload
systemctl enable "$SILVERTASK_SERVICE_NAME" >> "$SILVERTASK_LOG_FILE" 2>&1

# --- nginx ---
st_step "Configuring nginx"
NGINX_SERVER_NAME="${DOMAIN:-_}"
if [ -n "$DOMAIN" ] && [ "$SKIP_SSL" = false ]; then
    cat > /etc/nginx/sites-available/silvertask <<EOF
server {
    listen $HTTP_PORT;
    server_name $NGINX_SERVER_NAME;
    return 301 https://\$host\$request_uri;
}
server {
    listen $HTTPS_PORT ssl;
    http2 on;
    server_name $NGINX_SERVER_NAME;
    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    client_max_body_size 100M;
    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_set_header X-Real-IP \$remote_addr;
    }
    location /hubs/notifications {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_read_timeout 100s;
    }
}
EOF
else
    # HTTP-only (no domain, or --skip-ssl) — plain proxy, no redirect-to-HTTPS.
    cat > /etc/nginx/sites-available/silvertask <<EOF
server {
    listen $HTTP_PORT;
    server_name $NGINX_SERVER_NAME;
    client_max_body_size 100M;
    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_set_header X-Real-IP \$remote_addr;
    }
    location /hubs/notifications {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_read_timeout 100s;
    }
}
EOF
fi
ln -sf /etc/nginx/sites-available/silvertask /etc/nginx/sites-enabled/silvertask
rm -f /etc/nginx/sites-enabled/default
nginx -t >> "$SILVERTASK_LOG_FILE" 2>&1 || st_fail "nginx configuration test failed." "Check $SILVERTASK_LOG_FILE."

# --- Firewall ---
if [ "$SKIP_FIREWALL" = false ]; then
    st_step "Configuring firewall (ufw)"
    ufw allow OpenSSH >> "$SILVERTASK_LOG_FILE" 2>&1 || true
    ufw allow "$HTTP_PORT"/tcp >> "$SILVERTASK_LOG_FILE" 2>&1
    [ -n "$DOMAIN" ] && [ "$SKIP_SSL" = false ] && ufw allow "$HTTPS_PORT"/tcp >> "$SILVERTASK_LOG_FILE" 2>&1
    ufw --force enable >> "$SILVERTASK_LOG_FILE" 2>&1
    st_info "Firewall enabled — only OpenSSH, $HTTP_PORT/tcp$([ -n "$DOMAIN" ] && [ "$SKIP_SSL" = false ] && echo ", $HTTPS_PORT/tcp") allowed. PostgreSQL (5432) is not exposed."
else
    st_warn "Firewall configuration skipped (--skip-firewall). Ensure ports are otherwise restricted."
fi

# --- Start services, get a certificate, then reload nginx with it ---
st_step "Starting Silver Task"
systemctl restart "$SILVERTASK_SERVICE_NAME"
systemctl reload nginx

if [ -n "$DOMAIN" ] && [ "$SKIP_SSL" = false ]; then
    st_step "Requesting a Let's Encrypt certificate for $DOMAIN"
    st_warn "This requires $DOMAIN to already resolve (DNS) to this server's public IP — certbot will fail otherwise, and installation will continue without HTTPS in that case."
    apt-get install -y -qq certbot python3-certbot-nginx >> "$SILVERTASK_LOG_FILE" 2>&1
    if certbot --nginx -d "$DOMAIN" --non-interactive --agree-tos --register-unsafely-without-email --redirect >> "$SILVERTASK_LOG_FILE" 2>&1; then
        st_info "Certificate obtained and nginx reloaded."
    else
        st_warn "certbot failed (see $SILVERTASK_LOG_FILE) — likely DNS for $DOMAIN doesn't point here yet. Fix DNS and re-run: sudo certbot --nginx -d $DOMAIN"
        SKIP_SSL=true
    fi
fi

# --- Health check ---
st_step "Health check"
HEALTH_URL="http://127.0.0.1:$HTTP_PORT"
if ! st_health_check "$HEALTH_URL" 15 3; then
    st_fail "Application did not become healthy after installation." \
        "Check: systemctl status $SILVERTASK_SERVICE_NAME, journalctl -u $SILVERTASK_SERVICE_NAME -n 100, and $SILVERTASK_LOG_FILE"
fi

# --- Administrator account ---
# POST /api/users (Controllers/UsersController.cs) is open to anonymous requests only while
# zero users exist in the system, and that first account is always forced to Administrator
# regardless of the Role field sent — this is the application's own bootstrap rule, there is
# no separate "make me admin" step. Talks directly to Kestrel on its fixed internal loopback
# address (deploy/silvertask.service: ASPNETCORE_URLS=http://127.0.0.1:5000), not through
# nginx on $HTTP_PORT, so this works identically regardless of domain/HTTPS/redirect config.
ADMIN_CREATED=false
if [ -n "$ADMIN_EMAIL" ]; then
    st_step "Creating administrator account"
    ADMIN_PASSWORD="$(st_generate_password 20)"
    ADMIN_PAYLOAD="$(printf '{"name":"%s","email":"%s","password":"%s"}' \
        "$(st_json_escape "$ADMIN_NAME")" "$(st_json_escape "$ADMIN_EMAIL")" "$(st_json_escape "$ADMIN_PASSWORD")")"
    ADMIN_HTTP_STATUS="$(curl -sS -o /dev/null -w '%{http_code}' -X POST "http://127.0.0.1:5000/api/users" \
        -H "Content-Type: application/json" -d "$ADMIN_PAYLOAD" 2>>"$SILVERTASK_LOG_FILE" || true)"
    case "$ADMIN_HTTP_STATUS" in
        200|201)
            st_info "Administrator account created for $ADMIN_EMAIL."
            ADMIN_CREATED=true
            ;;
        401|403)
            st_info "An account already exists — skipping automatic administrator creation (expected on a re-run)."
            ;;
        *)
            st_warn "Could not automatically create the administrator account (HTTP ${ADMIN_HTTP_STATUS:-no response}). See $SILVERTASK_LOG_FILE."
            st_warn "Create one manually: curl -X POST http://127.0.0.1:5000/api/users -H 'Content-Type: application/json' -d '{\"name\":\"...\",\"email\":\"...\",\"password\":\"...\"}'"
            ;;
    esac
fi

SCHEME="http"; PORT_SUFFIX=":$HTTP_PORT"
if [ -n "$DOMAIN" ] && [ "$SKIP_SSL" = false ]; then
    SCHEME="https"; PORT_SUFFIX=""; [ "$HTTPS_PORT" != "443" ] && PORT_SUFFIX=":$HTTPS_PORT"
elif [ "$HTTP_PORT" = "80" ]; then
    PORT_SUFFIX=""
fi
FINAL_HOST="${DOMAIN:-$(hostname -I 2>/dev/null | awk '{print $1}')}"
FINAL_HOST="${FINAL_HOST:-localhost}"
FINAL_URL="$SCHEME://$FINAL_HOST$PORT_SUFFIX"

echo
st_info "=================================================================="
st_info " Silver Task installed successfully"
st_info "=================================================================="
st_info " URL:              $FINAL_URL"
st_info " Install directory: $SILVERTASK_INSTALL_DIR"
st_info " Environment file:  $SILVERTASK_ENV_FILE"
st_info " File storage:      $STORAGE_ROOT"
st_info " Service:           systemctl status $SILVERTASK_SERVICE_NAME"
st_info " Logs:              journalctl -u $SILVERTASK_SERVICE_NAME -f"
st_info "=================================================================="
if [ "$ADMIN_CREATED" = true ]; then
    # Deliberately plain `echo`, not st_info/st_warn — those also write to
    # $SILVERTASK_LOG_FILE, and this password is never written to any file by this script
    # (same "never log secrets" rule already applied to the DB password and JWT secret above).
    echo
    echo "=================================================================="
    echo " Administrator account created — shown once, save it now:"
    echo "=================================================================="
    echo " Email:    $ADMIN_EMAIL"
    echo " Password: $ADMIN_PASSWORD"
    echo "=================================================================="
    echo " Log in at $FINAL_URL. You can change this password anytime after"
    echo " logging in, or from another admin account via Admin -> Users."
    echo "=================================================================="
else
    st_info ""
    st_info " NEXT STEP: create your administrator account, then log in at $FINAL_URL:"
    st_info "   curl -X POST http://127.0.0.1:5000/api/users -H 'Content-Type: application/json' \\"
    st_info "     -d '{\"name\":\"Your Name\",\"email\":\"you@example.com\",\"password\":\"a-strong-password\"}'"
    st_info " This only works for the very first account — Silver Task automatically grants it"
    st_info " Administrator, with no separate admin-creation step. Every account after that is"
    st_info " created from inside the app by an existing Administrator (Admin -> Users)."
    st_info "=================================================================="
fi
