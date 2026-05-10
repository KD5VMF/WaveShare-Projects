#!/usr/bin/env bash
sudo apt update
sudo apt install -y python3-pil python3-spidev python3-numpy python3-rpi.gpio iputils-ping
sudo mkdir -p /opt/cm4-lcd-suite/common
sudo cp _common/cm4_lcd_common.py /opt/cm4-lcd-suite/common/
