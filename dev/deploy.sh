#!/usr/bin/env bash
# =============================================================================
# deploy.sh — Deploy RabiRiichi.Server as a systemd service
#
# Usage:
#   ./deploy.sh [OPTIONS]
#
# Options:
#   -u, --user        Dedicated OS user to run the service (default: riichi)
#   -d, --dir         Installation directory          (default: /opt/rabiriichi)
#   -r, --repo        GitHub repository slug          (default: RabiMimi/RabiRiichi)
#   -t, --tag         Release tag to deploy           (default: latest)
#   -p, --port        ASPNETCORE_URLS value           (default: http://0.0.0.0:5000)
#   -e, --env-file    Path to an extra environment file to install
#                     (key=value pairs, placed at /etc/rabiriichi/environment)
#   -k, --jwt-key     JWT secret key (default: auto-generate if not already set)
#   --undeploy        Stop and completely remove the service, user, and files
#   -h, --help        Show this help and exit
#
# Requirements (installed on the target server):
#   - bash, curl, unzip, systemctl (systemd)
#   - .NET 9 runtime (installed automatically if missing)
#
# Run as root or a sudo-capable user.
# =============================================================================

set -euo pipefail

# ─── Defaults ────────────────────────────────────────────────────────────────
SERVICE_USER="riichi"
INSTALL_DIR="/opt/rabiriichi"
GITHUB_REPO="RabiMimi/RabiRiichi"  # override with --repo if needed
RELEASE_TAG="latest"
APP_URLS="http://0.0.0.0:5000"
EXTRA_ENV_FILE=""
JWT_KEY=""
UNDEPLOY=false

SERVICE_NAME="rabiriichi"
CONFIG_DIR="/etc/rabiriichi"
ENV_FILE="${CONFIG_DIR}/environment"

# ─── Colours ─────────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'
info()    { echo -e "${CYAN}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[OK]${NC}    $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*"; }
die()     { echo -e "${RED}[ERROR]${NC} $*" >&2; exit 1; }

# ─── Parse arguments ─────────────────────────────────────────────────────────
usage() {
    grep '^#' "$0" | sed 's/^# \{0,1\}//' | tail -n +2 | head -n 30
    exit 0
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -u|--user)      SERVICE_USER="$2"; shift 2 ;;
        -d|--dir)       INSTALL_DIR="$2";  shift 2 ;;
        -r|--repo)      GITHUB_REPO="$2";  shift 2 ;;
        -t|--tag)       RELEASE_TAG="$2";  shift 2 ;;
        -p|--port)      APP_URLS="$2";     shift 2 ;;
        -e|--env-file)  EXTRA_ENV_FILE="$2"; shift 2 ;;
        -k|--jwt-key)   JWT_KEY="$2";      shift 2 ;;
        --undeploy)     UNDEPLOY=true; shift ;;
        -h|--help)      usage ;;
        *) die "Unknown option: $1. Run with --help for usage." ;;
    esac
done

# ─── Privilege check ─────────────────────────────────────────────────────────
if [[ $EUID -ne 0 ]]; then
    die "This script must be run as root (or via sudo)."
fi

# ─── Undeploy ────────────────────────────────────────────────────────────────
if [[ "$UNDEPLOY" == true ]]; then
    warn "Undeploying RabiRiichi.Server..."

    UNIT_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

    if systemctl is-active --quiet "${SERVICE_NAME}" 2>/dev/null; then
        info "Stopping service '${SERVICE_NAME}'..."
        systemctl stop "${SERVICE_NAME}"
    fi

    if systemctl is-enabled --quiet "${SERVICE_NAME}" 2>/dev/null; then
        info "Disabling service '${SERVICE_NAME}'..."
        systemctl disable "${SERVICE_NAME}"
    fi

    if [[ -f "${UNIT_FILE}" ]]; then
        info "Removing systemd unit ${UNIT_FILE}..."
        rm -f "${UNIT_FILE}"
        systemctl daemon-reload
        systemctl reset-failed "${SERVICE_NAME}" 2>/dev/null || true
    fi

    if [[ -d "${INSTALL_DIR}" ]]; then
        info "Removing install directory ${INSTALL_DIR}..."
        rm -rf "${INSTALL_DIR}"
    fi

    if [[ -d "${CONFIG_DIR}" ]]; then
        info "Removing config directory ${CONFIG_DIR}..."
        rm -rf "${CONFIG_DIR}"
    fi

    if id -u "${SERVICE_USER}" &>/dev/null; then
        info "Removing system user '${SERVICE_USER}'..."
        userdel "${SERVICE_USER}"
    fi

    echo ""
    success "RabiRiichi.Server has been fully undeployed."
    exit 0
fi

# ─── Resolve release tag ─────────────────────────────────────────────────────
RELEASE_API="https://api.github.com/repos/${GITHUB_REPO}/releases"
if [[ "$RELEASE_TAG" == "latest" ]]; then
    info "Fetching latest release tag from GitHub..."
    RELEASE_TAG=$(curl -fsSL "${RELEASE_API}/latest" \
        -H "Accept: application/vnd.github+json" \
        | grep -oP '"tag_name"\s*:\s*"\K[^"]+')
    [[ -n "$RELEASE_TAG" ]] || die "Failed to determine latest release tag."
    info "Latest release tag: ${RELEASE_TAG}"
fi

# Strip leading 'v' for the filename pattern used in release.yml
TAG_BARE="${RELEASE_TAG#v}"
ASSET_NAME="RabiRiichi.Server-${RELEASE_TAG}.zip"

# Build download URL
DOWNLOAD_URL=$(curl -fsSL "${RELEASE_API}/tags/${RELEASE_TAG}" \
    -H "Accept: application/vnd.github+json" \
    | grep -oP '"browser_download_url"\s*:\s*"\K[^"]+' \
    | grep "${ASSET_NAME}" || true)

[[ -n "$DOWNLOAD_URL" ]] || die "Asset '${ASSET_NAME}' not found in release '${RELEASE_TAG}'."

# ─── Install .NET 9 runtime if missing ───────────────────────────────────────
install_dotnet() {
    info "Installing .NET 9 runtime..."
    # Detect distro
    if command -v apt-get &>/dev/null; then
        # Debian / Ubuntu
        curl -fsSL https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb \
            -o /tmp/packages-microsoft-prod.deb
        dpkg -i /tmp/packages-microsoft-prod.deb
        rm /tmp/packages-microsoft-prod.deb
        apt-get update -qq
        apt-get install -y aspnetcore-runtime-9.0
    elif command -v dnf &>/dev/null; then
        # RHEL / Fedora / Rocky
        dnf install -y aspnetcore-runtime-9.0
    elif command -v zypper &>/dev/null; then
        # openSUSE
        zypper install -y aspnetcore-runtime-9.0
    else
        warn "Unknown package manager. Installing .NET via dotnet-install.sh..."
        curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
        chmod +x /tmp/dotnet-install.sh
        /tmp/dotnet-install.sh --channel 9.0 --runtime aspnetcore \
            --install-dir /usr/share/dotnet
        ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
        rm /tmp/dotnet-install.sh
    fi
}

if ! command -v dotnet &>/dev/null || ! dotnet --list-runtimes 2>/dev/null | grep -q "^Microsoft.AspNetCore.App 9\."; then
    install_dotnet
else
    DOTNET_VER=$(dotnet --version 2>/dev/null || echo "unknown")
    success ".NET runtime found (version: ${DOTNET_VER})"
fi

# ─── Create dedicated service user ───────────────────────────────────────────
if ! id -u "$SERVICE_USER" &>/dev/null; then
    info "Creating system user '${SERVICE_USER}'..."
    useradd --system \
        --shell /usr/sbin/nologin \
        --home-dir "${INSTALL_DIR}" \
        --no-create-home \
        --comment "RabiRiichi Server" \
        "$SERVICE_USER"
    success "User '${SERVICE_USER}' created."
else
    info "User '${SERVICE_USER}' already exists."
fi

# ─── Prepare directories ─────────────────────────────────────────────────────
info "Preparing directories..."
mkdir -p "${INSTALL_DIR}" "${CONFIG_DIR}"
chown "${SERVICE_USER}:${SERVICE_USER}" "${INSTALL_DIR}"
chmod 750 "${INSTALL_DIR}"
chown root:root "${CONFIG_DIR}"
chmod 755 "${CONFIG_DIR}"

# ─── Stop existing service (if running) ──────────────────────────────────────
if systemctl is-active --quiet "${SERVICE_NAME}" 2>/dev/null; then
    info "Stopping existing service '${SERVICE_NAME}'..."
    systemctl stop "${SERVICE_NAME}"
fi

# ─── Download and extract release ────────────────────────────────────────────
TMPDIR=$(mktemp -d)
trap 'rm -rf "$TMPDIR"' EXIT

info "Downloading ${ASSET_NAME}..."
curl -fsSL "${DOWNLOAD_URL}" -o "${TMPDIR}/${ASSET_NAME}"
success "Downloaded ${ASSET_NAME}."

info "Extracting to ${INSTALL_DIR}..."
# Wipe old binaries but keep config files
find "${INSTALL_DIR}" -mindepth 1 \
    ! -name 'appsettings.Production.json' \
    -delete 2>/dev/null || true

unzip -q "${TMPDIR}/${ASSET_NAME}" -d "${INSTALL_DIR}"
chown -R "${SERVICE_USER}:${SERVICE_USER}" "${INSTALL_DIR}"
chmod -R 750 "${INSTALL_DIR}"
success "Extraction complete."

# ─── Install environment configuration ───────────────────────────────────────
info "Writing environment config to ${ENV_FILE}..."

# Retrieve existing JWT_SECRET if the environment file already exists
EXISTING_JWT=""
if [[ -f "${ENV_FILE}" ]]; then
    EXISTING_JWT=$(grep -oP '^JWT_SECRET=\K.*' "${ENV_FILE}" || true)
fi

# Determine the JWT secret to use
FINAL_JWT=""
if [[ -n "$JWT_KEY" ]]; then
    FINAL_JWT="$JWT_KEY"
elif [[ -n "$EXISTING_JWT" ]]; then
    FINAL_JWT="$EXISTING_JWT"
    info "Retained existing JWT_SECRET from ${ENV_FILE}"
else
    info "Generating a random 64-character JWT secret..."
    if command -v openssl &>/dev/null; then
        FINAL_JWT=$(openssl rand -base64 48 | tr -d '\n\r')
    else
        FINAL_JWT=$(head -c 48 /dev/urandom | base64 | tr -d '\n\r')
    fi
fi

# Write base environment variables
cat > "${ENV_FILE}" <<EOF
# RabiRiichi Server — environment configuration
# Managed by deploy.sh — edit carefully.
# See: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration

ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=${APP_URLS}

# JWT secret key used by TokenService
JWT_SECRET=${FINAL_JWT}

# Example: override a setting from appsettings.json
# Logging__LogLevel__Default=Warning
EOF

# Merge extra env file provided via --env-file
if [[ -n "$EXTRA_ENV_FILE" ]]; then
    if [[ -f "$EXTRA_ENV_FILE" ]]; then
        info "Merging extra environment file: ${EXTRA_ENV_FILE}"
        echo "" >> "${ENV_FILE}"
        echo "# --- merged from ${EXTRA_ENV_FILE} ---" >> "${ENV_FILE}"
        cat "$EXTRA_ENV_FILE" >> "${ENV_FILE}"
    else
        warn "--env-file '${EXTRA_ENV_FILE}' not found; skipping."
    fi
fi

chmod 640 "${ENV_FILE}"
chown root:"${SERVICE_USER}" "${ENV_FILE}"
success "Environment file written."

# ─── Install systemd unit ────────────────────────────────────────────────────
UNIT_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
info "Installing systemd unit at ${UNIT_FILE}..."

# Find the server executable (the publish output name matches the project name)
SERVER_EXEC="${INSTALL_DIR}/RabiRiichi.Server"

cat > "${UNIT_FILE}" <<EOF
[Unit]
Description=RabiRiichi Game Server
Documentation=https://github.com/${GITHUB_REPO}
After=network-online.target
Wants=network-online.target

[Service]
Type=exec
User=${SERVICE_USER}
Group=${SERVICE_USER}
WorkingDirectory=${INSTALL_DIR}
ExecStart=${SERVER_EXEC}
Restart=on-failure
RestartSec=5s
KillSignal=SIGINT
SyslogIdentifier=${SERVICE_NAME}

# Load environment variables
EnvironmentFile=${ENV_FILE}

# Harden the process
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=full
ProtectHome=true
ReadWritePaths=${INSTALL_DIR}

# Resource limits
LimitNOFILE=65536

[Install]
WantedBy=multi-user.target
EOF

chmod 644 "${UNIT_FILE}"
success "systemd unit installed."

# ─── Enable and start service ─────────────────────────────────────────────────
info "Reloading systemd daemon..."
systemctl daemon-reload

info "Enabling '${SERVICE_NAME}' to start on boot..."
systemctl enable "${SERVICE_NAME}"

info "Starting '${SERVICE_NAME}'..."
systemctl start "${SERVICE_NAME}"

# ─── Verify ──────────────────────────────────────────────────────────────────
sleep 2
if systemctl is-active --quiet "${SERVICE_NAME}"; then
    success "Service '${SERVICE_NAME}' is running."
else
    die "Service '${SERVICE_NAME}' failed to start. Check logs with: journalctl -u ${SERVICE_NAME} -n 50"
fi

echo ""
echo -e "${GREEN}╔══════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║  RabiRiichi.Server deployed successfully! 🐰     ║${NC}"
echo -e "${GREEN}╠══════════════════════════════════════════════════╣${NC}"
echo -e "${GREEN}║${NC}  Version  : ${RELEASE_TAG}"
echo -e "${GREEN}║${NC}  User     : ${SERVICE_USER}"
echo -e "${GREEN}║${NC}  Install  : ${INSTALL_DIR}"
echo -e "${GREEN}║${NC}  Config   : ${ENV_FILE}"
echo -e "${GREEN}║${NC}  Service  : ${SERVICE_NAME}"
echo -e "${GREEN}║${NC}  URL      : ${APP_URLS}"
echo -e "${GREEN}╠══════════════════════════════════════════════════╣${NC}"
echo -e "${GREEN}║${NC}  Useful commands:"
echo -e "${GREEN}║${NC}    systemctl status  ${SERVICE_NAME}"
echo -e "${GREEN}║${NC}    journalctl -u ${SERVICE_NAME} -f"
echo -e "${GREEN}║${NC}    systemctl restart ${SERVICE_NAME}"
echo -e "${GREEN}╚══════════════════════════════════════════════════╝${NC}"
