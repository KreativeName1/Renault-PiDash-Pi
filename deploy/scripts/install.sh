#!/usr/bin/env bash
set -euo pipefail

sudo useradd -r -s /usr/sbin/nologin pidash || true
sudo mkdir -p /opt/pidash /var/lib/pidash
sudo chown -R pidash:pidash /var/lib/pidash

sudo cp deploy/systemd/pidash.service /etc/systemd/system/pidash.service
sudo systemctl daemon-reload
sudo systemctl enable pidash
echo "Installed service. Use update.sh to deploy binaries."
