#!/usr/bin/env bash
set -euo pipefail

systemctl stop cm4-update-lcd dual-pendulum-lcd helix-solar-lcd biogenesis-life-lcd ai-chess-lcd pfsense-lcd ai-bricks-lcd wifi-radar-lcd tpot-lcd adguard-lcd packet-storm-lcd tpot-map-lcd cyber-threat-lcd cm4-ai-bricks 2>/dev/null || true
systemctl restart dual-dns-lcd
systemctl status dual-dns-lcd --no-pager -l
