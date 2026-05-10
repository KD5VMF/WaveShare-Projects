#!/usr/bin/env bash
set -euo pipefail
sudo apt update
sudo apt install -y python3-pil python3-spidev python3-numpy python3-rpi.gpio stockfish python3-venv python3-pip
sudo mkdir -p /opt/cm4-lcd-suite/common /opt/ai-chess-lcd
sudo cp ../../_common/cm4_lcd_common.py /opt/cm4-lcd-suite/common/
sudo cp ai_chess_lcd.py /opt/ai-chess-lcd/
sudo cp ai-chess-lcd.env.example /etc/default/ai-chess-lcd
sudo cp ai-chess-lcd.service /etc/systemd/system/ai-chess-lcd.service
sudo rm -rf /opt/ai-chess-lcd/venv /opt/ai-chess-lcd/__pycache__
sudo python3 -m venv --system-site-packages /opt/ai-chess-lcd/venv
sudo /opt/ai-chess-lcd/venv/bin/pip install --upgrade pip
sudo /opt/ai-chess-lcd/venv/bin/pip install chess
sudo sed -i 's#ExecStart=/usr/bin/python3 -u#ExecStart=/opt/ai-chess-lcd/venv/bin/python -u#' /etc/systemd/system/ai-chess-lcd.service
sudo chmod 600 /etc/default/ai-chess-lcd
sudo chmod +x /opt/ai-chess-lcd/ai_chess_lcd.py
sudo systemctl daemon-reload
