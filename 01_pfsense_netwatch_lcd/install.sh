#!/usr/bin/env bash
set -euo pipefail
bash ../../_common/install_app_common.sh pfsense-lcd pfsense_lcd.py pfsense-lcd.env.example pfsense-lcd.service "snmp"
