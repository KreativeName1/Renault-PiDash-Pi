# This script builds the PiDash project and deploys it to a Raspberry Pi over SSH.
# Run this from the dev machine, passing the Pi's SSH connection details and the path to the project.
# This is the Linux/Mac version; see deployToPi.ps1 for a Windows version.

#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  deploy.sh --host <pi-host> --user <pi-user> --project-dir <repo-root> [options]

Required:
  --host            Pi hostname or IP
  --user            SSH username on Pi
  --project-dir     Repo root folder containing 'src'

Options:
  --rid             linux-arm64 | linux-arm   (default: linux-arm64)
  --configuration   Build configuration        (default: Release)
  --service-name    systemd service name       (default: pidash)
  --stage-dir       Remote stage dir           (default: /tmp/pidash-publish)
  --install-dir     Remote install dir         (default: /opt/pidash)
  --ssh-key         Path to SSH private key    (default: empty)
  -h, --help        Show this help
EOF
}

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "Required command not found: $1" >&2; exit 1; }
}

# Defaults
RID="linux-arm64"
CONFIGURATION="Release"
SERVICE_NAME="pidash"
REMOTE_STAGE_DIR="/tmp/pidash-publish"
REMOTE_INSTALL_DIR="/opt/pidash"
SSH_KEY_PATH=""

PI_HOST=""
PI_USER=""
PROJECT_DIR=""

# Parse args
while [[ $# -gt 0 ]]; do
  case "$1" in
    --host)          PI_HOST="${2:-}"; shift 2 ;;
    --user)          PI_USER="${2:-}"; shift 2 ;;
    --project-dir)   PROJECT_DIR="${2:-}"; shift 2 ;;
    --rid)           RID="${2:-}"; shift 2 ;;
    --configuration) CONFIGURATION="${2:-}"; shift 2 ;;
    --service-name)  SERVICE_NAME="${2:-}"; shift 2 ;;
    --stage-dir)     REMOTE_STAGE_DIR="${2:-}"; shift 2 ;;
    --install-dir)   REMOTE_INSTALL_DIR="${2:-}"; shift 2 ;;
    --ssh-key)       SSH_KEY_PATH="${2:-}"; shift 2 ;;
    -h|--help)       usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage; exit 2 ;;
  esac
done

# Validate required args
if [[ -z "$PI_HOST" || -z "$PI_USER" || -z "$PROJECT_DIR" ]]; then
  echo "Missing required arguments." >&2
  usage
  exit 2
fi

# Validate RID
case "$RID" in
  linux-arm64|linux-arm) ;;
  *) echo "Invalid --rid: $RID (must be linux-arm64 or linux-arm)" >&2; exit 2 ;;
esac

require_cmd dotnet
require_cmd ssh
require_cmd scp
require_cmd realpath

PROJECT_DIR_FULL="$(realpath "$PROJECT_DIR")"
SRC_DIR="$PROJECT_DIR_FULL/src"

if [[ ! -d "$SRC_DIR" ]]; then
  echo "Could not find '$SRC_DIR'. Pass the repo root folder that contains 'src'." >&2
  exit 1
fi

echo
echo "=== Building PiDash ($CONFIGURATION, $RID) ==="

pushd "$SRC_DIR" >/dev/null
dotnet publish -c "$CONFIGURATION" -r "$RID" --self-contained false
popd >/dev/null

PUBLISH_DIR="$SRC_DIR/bin/$CONFIGURATION/net10.0/$RID/publish"
if [[ ! -d "$PUBLISH_DIR" ]]; then
  echo "Publish directory not found: $PUBLISH_DIR" >&2
  exit 1
fi

echo
echo "=== Uploading to Pi ==="

SSH_TARGET="${PI_USER}@${PI_HOST}"

SSH_ARGS=()
if [[ -n "$SSH_KEY_PATH" ]]; then
  SSH_ARGS+=(-i "$SSH_KEY_PATH")
fi

# Create/clean stage dir
ssh "${SSH_ARGS[@]}" "$SSH_TARGET" "sudo rm -rf '$REMOTE_STAGE_DIR' && sudo mkdir -p '$REMOTE_STAGE_DIR'"

# Stream publish output as tar over ssh into the stage dir
tar -C "$PUBLISH_DIR" -czf - . \
  | ssh "${SSH_ARGS[@]}" "$SSH_TARGET" "sudo tar -xzf - -C '$REMOTE_STAGE_DIR'"

echo
echo "=== Deploying & restarting service ==="

ssh "${SSH_ARGS[@]}" "$SSH_TARGET" bash -s -- \
  "$SERVICE_NAME" "$REMOTE_STAGE_DIR" "$REMOTE_INSTALL_DIR" <<'REMOTE_SCRIPT'
set -e
SERVICE_NAME="$1"
REMOTE_STAGE_DIR="$2"
REMOTE_INSTALL_DIR="$3"

sudo systemctl stop "$SERVICE_NAME" || true
sudo mkdir -p "$REMOTE_INSTALL_DIR"
sudo rm -rf "$REMOTE_INSTALL_DIR"/*
sudo cp -a "$REMOTE_STAGE_DIR"/. "$REMOTE_INSTALL_DIR"/.
sudo chmod -R a+rX "$REMOTE_INSTALL_DIR"
sudo systemctl start "$SERVICE_NAME"
sudo systemctl status "$SERVICE_NAME" --no-pager
REMOTE_SCRIPT

echo
echo "✅ Deploy complete. Tail logs with:"
echo "ssh ${SSH_TARGET} 'sudo journalctl -u ${SERVICE_NAME} -f'"
