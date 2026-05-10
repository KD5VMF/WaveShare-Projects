#!/usr/bin/env bash
set -euo pipefail

echo "This will run apt upgrades and reboot when finished."
echo "Press Ctrl+C now to cancel."
sleep 5
systemctl start cm4-safe-nightly-update.service
