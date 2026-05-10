#!/usr/bin/env bash
set -euo pipefail
bash ../../_common/install_app_common.sh tpot-lcd tpot_lcd.py tpot-lcd.env.example tpot-lcd.service "openssh-client"
