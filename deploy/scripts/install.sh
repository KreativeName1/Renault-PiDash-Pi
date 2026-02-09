#!/usr/bin/env bash
set -euo pipefail
APP_USER="pidash"
APP_GROUP="pidash"
APP_DIR="/opt/pidash"
DATA_DIR="/var/lib/pidash"

# Custom temp folder for large downloads (prefer a roomy location)
# You can override via: INSTALL_TEMP_DIR=/path/with/space ./install.sh
INSTALL_TEMP_DIR_DEFAULT="${HOME}/.pidash-install-tmp"

# .NET install target
DOTNET_ROOT="/opt/dotnet"
DOTNET_CHANNEL="10.0"        # Major version (10 is latest LTS; 9 is older LTS)
DOTNET_VERSION="latest"      # Resolve current patch (e.g., 10.0.102)
DOTNET_INSTALL_URL="https://dot.net/v1/dotnet-install.sh"
DOTNET_AZURE_FEED="https://dotnetcli.azureedge.net/dotnet"

# ---------- helpers ----------
log() { echo -e "\n[pidash-install] $*"; }

need_cmd() { command -v "$1" >/dev/null 2>&1; }

append_if_missing() {
  local file="$1"
  local line="$2"
  if ! grep -qE "^\s*${line//\//\\/}\s*$" "$file" 2>/dev/null; then
    echo "$line" | sudo tee -a "$file" >/dev/null
    return 0
  fi
  return 1
}

# ---------- 0) sanity ----------
if [[ "$(id -u)" -eq 0 ]]; then
  log "Please run this script as a normal user (it uses sudo internally)."
  exit 1
fi

if ! need_cmd sudo; then
  echo "sudo is required."
  exit 1
fi

# ---------- 1) base packages ----------
#log "Installing base packages..."
#sudo apt-get update
#sudo apt-get install -y \
#  ca-certificates \
#  curl \
#  rsync \
#  sqlite3

# ---------- 2) install Git ----------
if ! need_cmd git; then
  log "Installing git..."
  sudo apt-get install -y git
fi

# ---------- 3) install .NET SDK (for compiling and running apps) ----------
log "Installing .NET SDK into ${DOTNET_ROOT} (channel ${DOTNET_CHANNEL})..."

sudo mkdir -p "${DOTNET_ROOT}"
sudo chown -R root:root "${DOTNET_ROOT}"
sudo chmod -R a+rX "${DOTNET_ROOT}"

# Detect architecture
ARCH="$(uname -m)"
if [[ "$ARCH" == "aarch64" ]]; then
  SDK_ARCH="arm64"
  SDK_RID="linux-arm64"
else
  SDK_ARCH="arm"
  SDK_RID="linux-arm"
fi

# Select temp folder for downloads (has more space than /tmp on RPi)
TMP_DIR="${INSTALL_TEMP_DIR:-${INSTALL_TEMP_DIR_DEFAULT}}"

# If chosen location is low on space, fall back to /var/tmp if possible
SPACE_MIN_MB=500
if mkdir -p "${TMP_DIR}" 2>/dev/null; then
  AVAILABLE_KB=$(df "${TMP_DIR}" | awk 'NR==2 {print $4}')
  AVAILABLE_MB=$((AVAILABLE_KB / 1024))
else
  AVAILABLE_MB=0
fi

if [[ ${AVAILABLE_MB} -lt ${SPACE_MIN_MB} ]]; then
  if mkdir -p "/var/tmp/pidash-install" 2>/dev/null; then
    ALT_TMP_DIR="/var/tmp/pidash-install"
    ALT_AVAILABLE_KB=$(df "${ALT_TMP_DIR}" | awk 'NR==2 {print $4}')
    ALT_AVAILABLE_MB=$((ALT_AVAILABLE_KB / 1024))
    if [[ ${ALT_AVAILABLE_MB} -ge ${SPACE_MIN_MB} ]]; then
      TMP_DIR="${ALT_TMP_DIR}"
      AVAILABLE_MB=${ALT_AVAILABLE_MB}
    fi
  fi
fi

mkdir -p "${TMP_DIR}"
trap "rm -rf ${TMP_DIR}" EXIT

log "Using temp directory: ${TMP_DIR}"
log "Available space in ${TMP_DIR}: ${AVAILABLE_MB} MB"
if [[ ${AVAILABLE_MB} -lt ${SPACE_MIN_MB} ]]; then
  log "ERROR: Not enough space (need ~${SPACE_MIN_MB}MB, have ${AVAILABLE_MB}MB)"
  log "Try freeing up disk space or set INSTALL_TEMP_DIR to a larger location."
  log "Example: INSTALL_TEMP_DIR=/var/tmp/pidash-install ./install.sh"
  log "  sudo apt-get clean"
  log "  df -h"
  exit 1
fi

# Download and install SDK via dotnet-install (resolves latest patch)
log "Resolving latest .NET SDK version for channel ${DOTNET_CHANNEL}..."
SDK_VERSION="${DOTNET_VERSION}"
if [[ "${DOTNET_VERSION}" == "latest" ]]; then
  SDK_VERSION=$(curl -fSL "${DOTNET_AZURE_FEED}/Sdk/${DOTNET_CHANNEL}/latest.version")
fi

SDK_TARBALL="dotnet-sdk-${SDK_VERSION}-linux-${SDK_ARCH}.tar.gz"
SDK_DOWNLOAD_URL="${DOTNET_AZURE_FEED}/Sdk/${SDK_VERSION}/${SDK_TARBALL}"

log "Downloading SDK from ${SDK_DOWNLOAD_URL}..."
curl -fSL -o "${TMP_DIR}/${SDK_TARBALL}" --retry 5 --retry-delay 2 --retry-max-time 300 "${SDK_DOWNLOAD_URL}"

log "Extracting SDK to ${DOTNET_ROOT}..."
sudo tar -xzf "${TMP_DIR}/${SDK_TARBALL}" -C "${DOTNET_ROOT}"

sudo chown -R root:root "${DOTNET_ROOT}"
sudo chmod -R a+rX "${DOTNET_ROOT}"

# Always create symlink to /opt/dotnet/dotnet to ensure we use the runtime we just installed
log "Creating /usr/local/bin/dotnet symlink to ${DOTNET_ROOT}/dotnet..."
sudo rm -f /usr/local/bin/dotnet
sudo ln -sf "${DOTNET_ROOT}/dotnet" /usr/local/bin/dotnet

# Verify the symlink points to the correct runtime
log "Verifying dotnet symlink..."
ls -l /usr/local/bin/dotnet || true

log "dotnet version from /opt/dotnet:"
"${DOTNET_ROOT}/dotnet" --info || true

# ---------- 4) enable SPI (SPI0 + SPI1 overlays) ----------
# Raspberry Pi OS may use /boot/firmware/config.txt (newer) or /boot/config.txt (older). :contentReference[oaicite:2]{index=2}
BOOTCFG=""
if [[ -f /boot/firmware/config.txt ]]; then
  BOOTCFG="/boot/firmware/config.txt"
elif [[ -f /boot/config.txt ]]; then
  BOOTCFG="/boot/config.txt"
else
  log "WARNING: Could not find /boot/firmware/config.txt or /boot/config.txt. Skipping SPI enable."
fi

REBOOT_REQUIRED=0
if [[ -n "${BOOTCFG}" ]]; then
  log "Configuring SPI in ${BOOTCFG}..."

  # Ensure SPI0 enabled
  if append_if_missing "${BOOTCFG}" "dtparam=spi=on"; then
    REBOOT_REQUIRED=1
  fi

  # Enable SPI1 with 3 chip selects (creates /dev/spidev1.0 /dev/spidev1.1 /dev/spidev1.2). :contentReference[oaicite:3]{index=3}
  # If you want fewer CS lines, change to spi1-1cs or spi1-2cs.
  if append_if_missing "${BOOTCFG}" "dtoverlay=spi1-3cs"; then
    REBOOT_REQUIRED=1
  fi
fi

# ---------- 5) create user, dirs, permissions ----------
log "Creating service user and directories..."
sudo useradd -r -s /usr/sbin/nologin "${APP_USER}" || true
sudo groupadd -f "${APP_GROUP}" || true
sudo usermod -a -G "${APP_GROUP}" "${APP_USER}" || true

sudo mkdir -p "${APP_DIR}" "${DATA_DIR}"
sudo chown -R root:root "${APP_DIR}"
sudo chown -R "${APP_USER}:${APP_GROUP}" "${DATA_DIR}"
sudo chmod 0755 "${APP_DIR}"
sudo chmod 0750 "${DATA_DIR}"

# Add user to common hardware-access groups if they exist.
# On Raspberry Pi OS, /dev/spidev* is often group 'spi'. GPIO may be 'gpio'. Audio may be 'audio'.
for g in spi gpio i2c audio; do
  if getent group "$g" >/dev/null 2>&1; then
    sudo usermod -a -G "$g" "${APP_USER}" || true
  fi
done

log "Install complete."

# ---------- 6) next steps ----------
if [[ "${REBOOT_REQUIRED}" -eq 1 ]]; then
  log "A reboot is required for SPI changes to take effect."
  log "After reboot, verify SPI devices with: ls -l /dev/spidev*"
else
  log "SPI config unchanged (or boot config not found)."
fi

log "Next: deploy/compile the final app on the device with 'update_from_repo.sh'."
echo "  sudo /opt/pidash/update_from_repo.sh <repo-url> [branch]"
echo "Or: copy the published files into /opt/pidash/publish and then start the service."
echo "  sudo systemctl restart pidash || sudo systemctl start pidash"
