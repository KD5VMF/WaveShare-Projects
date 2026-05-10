#!/usr/bin/env bash
set -euo pipefail
bash ../../_common/install_app_common.sh wifi-radar-lcd wifi_radar_lcd.py wifi-radar-lcd.env.example wifi-radar-lcd.service "wireless-tools iw"
