#!/usr/bin/env bash
set -euo pipefail

echo "=== Installing CM4 Dual DNS LCD project ==="

apt update
apt install -y python3-pil python3-spidev python3-numpy python3-rpi.gpio iputils-ping dnsutils

mkdir -p /opt/dual-dns-lcd /opt/cm4-update-lcd

install -m 0755 src/dual_dns_lcd.py /opt/dual-dns-lcd/dual_dns_lcd.py
install -m 0755 src/update_lcd.py /opt/cm4-update-lcd/update_lcd.py

if [ ! -f /etc/default/dual-dns-lcd ]; then
  install -m 0600 defaults/dual-dns-lcd.example /etc/default/dual-dns-lcd
else
  echo "Keeping existing /etc/default/dual-dns-lcd"
fi

if [ ! -f /etc/default/cm4-update-lcd ]; then
  install -m 0644 defaults/cm4-update-lcd.example /etc/default/cm4-update-lcd
else
  echo "Keeping existing /etc/default/cm4-update-lcd"
fi

install -m 0644 systemd/dual-dns-lcd.service /etc/systemd/system/dual-dns-lcd.service
install -m 0644 systemd/cm4-update-lcd.service /etc/systemd/system/cm4-update-lcd.service

python3 -m py_compile /opt/dual-dns-lcd/dual_dns_lcd.py
python3 -m py_compile /opt/cm4-update-lcd/update_lcd.py

systemctl daemon-reload

echo "Install complete."
echo
echo "Start now:"
echo "  sudo systemctl restart dual-dns-lcd"
echo
echo "Start on boot:"
echo "  sudo ./scripts/set_dual_dns_boot.sh"
