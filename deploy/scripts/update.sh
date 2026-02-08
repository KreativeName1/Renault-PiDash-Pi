#!/usr/bin/env bash
set -euo pipefail

PUBLISH_DIR="${1:-src/bin/Release/net8.0/linux-arm/publish}"

if [ ! -d "$PUBLISH_DIR" ]; then
  echo "Publish dir not found: $PUBLISH_DIR"
  exit 1
fi

sudo systemctl stop pidash || true
sudo rsync -a --delete "$PUBLISH_DIR"/ /opt/pidash/
sudo systemctl start pidash
sudo systemctl status pidash --no-pager
