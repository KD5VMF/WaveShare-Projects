# CM4-NAS Waveshare LCD App Suite

GitHub-ready package with each LCD app in its own folder. No passwords, private keys, or SNMP secrets are included.

## Apps

1. `01_pfsense_netwatch_lcd` — pfSense RX/TX, ping, DNS, interface status.
2. `02_wifi_radar_lcd` — nearby Wi-Fi radar-style scanner.
3. `03_ai_bricks_lcd` — self-playing AI Bricks game.
4. `04_tpot_lcd` — T-Pot/tar-pit status over SSH.
5. `05_adguard_lcd` — AdGuard Home DNS/block dashboard.
6. `06_packet_storm_lcd` — pfSense traffic as rain/lightning.
7. `07_tpot_map_lcd` — abstract T-Pot world-map attack display.
8. `08_cyber_threat_lcd` — big color cyber threat meter.
9. `09_ai_chess_lcd` — Stockfish AI-vs-AI chess display.

## Install one app

```bash
cd 09_ai_chess_lcd
bash install.sh
sudo systemctl restart ai-chess-lcd
```

## Switch apps

Only one program can own the LCD GPIO/SPI pins at a time.

```bash
bash switch_lcd_app.sh ai-chess-lcd
bash switch_lcd_app.sh pfsense-lcd
bash switch_lcd_app.sh adguard-lcd
```

## Stop all LCD apps / fix GPIO busy

```bash
bash stop_all_lcd_apps.sh
```

If old bricks service grabs the LCD:

```bash
sudo systemctl stop cm4-ai-bricks.service
sudo systemctl disable cm4-ai-bricks.service
sudo systemctl mask cm4-ai-bricks.service
```

## Boot one app

```bash
sudo systemctl enable ai-chess-lcd
sudo systemctl disable pfsense-lcd ai-bricks-lcd wifi-radar-lcd tpot-lcd adguard-lcd packet-storm-lcd tpot-map-lcd cyber-threat-lcd
```

## T-Pot key setup

```bash
ssh-keygen -t ed25519 -f /home/sysop/.ssh/tpot_lcd_ed25519 -N "" -C "cm4-tpot-lcd"
ssh-copy-id -i /home/sysop/.ssh/tpot_lcd_ed25519.pub -p 64295 sysop@192.168.1.80
```

## Config files

Each app installs `/etc/default/<service-name>`. Edit those files for passwords/SNMP/community/IPs after install.
