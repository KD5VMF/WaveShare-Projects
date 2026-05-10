#!/usr/bin/env bash
set -euo pipefail
bash ../../_common/install_app_common.sh packet-storm-lcd packet_storm_lcd.py packet-storm-lcd.env.example packet-storm-lcd.service "snmp"
