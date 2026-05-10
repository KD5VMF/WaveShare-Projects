#!/usr/bin/env bash
sudo systemctl stop pfsense-lcd ai-bricks-lcd wifi-radar-lcd tpot-lcd adguard-lcd packet-storm-lcd tpot-map-lcd cyber-threat-lcd ai-chess-lcd cm4-ai-bricks.service 2>/dev/null || true
sudo pkill -9 -f 'LCD_2inch|lcdconfig|pfsense_lcd|ai_bricks_lcd|wifi_radar_lcd|tpot_lcd|tpot_map_lcd|adguard_lcd|packet_storm_lcd|cyber_threat_lcd|ai_chess_lcd|cm4_ai_bricks' 2>/dev/null || true
sleep 1
sudo fuser -v /dev/gpiochip* /dev/spidev* 2>/dev/null || true
