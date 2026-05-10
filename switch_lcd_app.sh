#!/usr/bin/env bash
set -euo pipefail
APP="${1:-}"
if [ -z "$APP" ]; then echo "Usage: $0 <service-name>"; exit 1; fi
sudo systemctl stop pfsense-lcd ai-bricks-lcd wifi-radar-lcd tpot-lcd adguard-lcd packet-storm-lcd tpot-map-lcd cyber-threat-lcd ai-chess-lcd cm4-ai-bricks.service 2>/dev/null || true
sudo systemctl restart "$APP"
sudo systemctl status "$APP" --no-pager -l
