#!/usr/bin/env bash
set -euo pipefail
APP="$1"; PY="$2"; ENV="$3"; SVC="$4"; EXTRA="${5:-}"
sudo apt update
sudo apt install -y python3-pil python3-spidev python3-numpy python3-rpi.gpio iputils-ping $EXTRA
sudo mkdir -p /opt/cm4-lcd-suite/common /opt/$APP
sudo cp ../../_common/cm4_lcd_common.py /opt/cm4-lcd-suite/common/
sudo cp "$PY" /opt/$APP/
sudo cp "$ENV" /etc/default/$APP
sudo cp "$SVC" /etc/systemd/system/$APP.service
sudo chmod 600 /etc/default/$APP || true
sudo chmod +x /opt/$APP/$PY
sudo systemctl daemon-reload
echo "Installed $APP"
