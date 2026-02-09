#!/usr/bin/env bash
set -euo pipefail

APP_USER="pidash"
APP_GROUP="pidash"
APP_DIR="/opt/pidash"
SRC_DIR="/opt/pidash-src"
PUBLISH_DIR="/opt/pidash/publish"

REPO_URL="${1:-}"
BRANCH="${2:-main}"

log() { echo -e "\n[pidash-update] $*"; }
need_cmd() { command -v "$1" >/dev/null 2>&1; }

if [[ "$(id -u)" -eq 0 ]]; then
  log "It's recommended to run this as a normal user; sudo will be used where needed."
fi

# Ensure git is available
if ! need_cmd git; then
  log "Installing git..."
  sudo apt-get update
  sudo apt-get install -y git
fi

ARCH="$(uname -m)"
if [[ "$ARCH" == "aarch64" ]]; then
  RID="linux-arm64"
else
  RID="linux-arm"
fi

# Clone or pull the repo into ${SRC_DIR}
if [[ -d "${SRC_DIR}/.git" ]]; then
  log "Updating existing source in ${SRC_DIR} (branch ${BRANCH})..."
  git -C "${SRC_DIR}" fetch --all --prune
  git -C "${SRC_DIR}" checkout "${BRANCH}" || git -C "${SRC_DIR}" checkout -b "${BRANCH}" "origin/${BRANCH}"
  git -C "${SRC_DIR}" pull --ff-only origin "${BRANCH}"
else
  if [[ -z "${REPO_URL}" ]]; then
    log "ERROR: No source found in ${SRC_DIR} and no REPO_URL supplied."
    log "Usage: sudo /opt/pidash/update_from_repo.sh <repo-url> [branch]"
    exit 2
  fi
  log "Cloning ${REPO_URL} (branch ${BRANCH}) into ${SRC_DIR}..."
  sudo rm -rf "${SRC_DIR}"
  sudo mkdir -p "${SRC_DIR}"
  sudo chown "${SUDO_USER:-$(whoami)}":"${SUDO_USER:-$(whoami)}" "${SRC_DIR}"
  git clone --branch "${BRANCH}" "${REPO_URL}" "${SRC_DIR}"
fi

# Build/publish the project. Expect project root under ${SRC_DIR}/src
if [[ -d "${SRC_DIR}/src" ]]; then
  BUILD_DIR="${SRC_DIR}/src"
else
  BUILD_DIR="${SRC_DIR}"
fi

log "Building (Release, ${RID}) from ${BUILD_DIR}..."
pushd "${BUILD_DIR}" >/dev/null
dotnet publish -c Release -r "${RID}" --self-contained false -o "${PUBLISH_DIR}"
popd >/dev/null

log "Deploying published files into ${PUBLISH_DIR} (owned by root)."
sudo mkdir -p "${PUBLISH_DIR}"
sudo chown -R root:root "${PUBLISH_DIR}"
sudo chmod -R a+rX "${PUBLISH_DIR}"

# Create systemd unit if missing
SERVICE_FILE="/etc/systemd/system/pidash.service"
if [[ ! -f "${SERVICE_FILE}" ]]; then
  log "Creating systemd unit at ${SERVICE_FILE}..."
  sudo tee "${SERVICE_FILE}" >/dev/null <<'UNIT'
[Unit]
Description=PiDash service
After=network.target

[Service]
WorkingDirectory=/opt/pidash
ExecStart=/usr/local/bin/dotnet /opt/pidash/publish/PiDash.dll
Restart=on-failure
RestartSec=5
User=pidash
Group=pidash
Environment=DOTNET_ROOT=/opt/dotnet

[Install]
WantedBy=multi-user.target
UNIT
  sudo mkdir -p /etc/systemd/system/pidash.service.d
  sudo tee /etc/systemd/system/pidash.service.d/10-dotnet.conf >/dev/null <<EOF
[Service]
Environment=DOTNET_ROOT=/opt/dotnet
Environment=PATH=/usr/local/bin:/usr/bin:/bin
EOF
  sudo systemctl daemon-reload
  sudo systemctl enable pidash
fi

# Restart the service
log "Restarting pidash service..."
sudo systemctl restart pidash || (sudo systemctl start pidash && true)

log "Update complete. View logs with: sudo journalctl -u pidash -f"
