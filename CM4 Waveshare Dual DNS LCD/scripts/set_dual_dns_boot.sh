#!/usr/bin/env bash
set -euo pipefail

systemctl enable dual-dns-lcd
systemctl disable dual-pendulum-lcd helix-solar-lcd biogenesis-life-lcd ai-chess-lcd pfsense-lcd ai-bricks-lcd wifi-radar-lcd tpot-lcd adguard-lcd packet-storm-lcd tpot-map-lcd cyber-threat-lcd cm4-ai-bricks 2>/dev/null || true
systemctl mask cm4-ai-bricks.service 2>/dev/null || true

systemctl stop dual-pendulum-lcd helix-solar-lcd biogenesis-life-lcd ai-chess-lcd pfsense-lcd ai-bricks-lcd wifi-radar-lcd tpot-lcd adguard-lcd packet-storm-lcd tpot-map-lcd cyber-threat-lcd cm4-ai-bricks 2>/dev/null || true
systemctl restart dual-dns-lcd

systemctl is-enabled dual-dns-lcd
systemctl status dual-dns-lcd --no-pager -l
