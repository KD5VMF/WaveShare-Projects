#!/usr/bin/env bash
set -euo pipefail

echo "TEST UPDATE SCREEN" > /run/cm4-safe-update.status
touch /var/log/cm4-safe-nightly-update.log
echo "Manual display test: $(date)" >> /var/log/cm4-safe-nightly-update.log
echo "This is only a screen test, not an update." >> /var/log/cm4-safe-nightly-update.log

systemctl stop dual-dns-lcd 2>/dev/null || true
systemctl start cm4-update-lcd
systemctl status cm4-update-lcd --no-pager -l
