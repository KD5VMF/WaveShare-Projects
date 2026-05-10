# Troubleshooting

## LCD says nothing or old app stays on screen

Check which LCD services are running:

```bash
systemctl --no-pager --type=service --state=running | egrep 'lcd|dns|bricks|chess|storm|tpot|adguard'
```

Stop conflicting services:

```bash
sudo systemctl stop dual-pendulum-lcd helix-solar-lcd biogenesis-life-lcd ai-chess-lcd pfsense-lcd ai-bricks-lcd wifi-radar-lcd tpot-lcd adguard-lcd packet-storm-lcd tpot-map-lcd cyber-threat-lcd cm4-ai-bricks 2>/dev/null || true
sudo systemctl restart dual-dns-lcd
```

## GPIO busy

Error example:

```text
lgpio.error: 'GPIO busy'
```

Another app owns the LCD GPIO pins.

Find users:

```bash
sudo fuser -v /dev/gpiochip* /dev/spidev* 2>/dev/null || true
```

Stop old services:

```bash
sudo systemctl stop cm4-ai-bricks.service 2>/dev/null || true
sudo systemctl disable cm4-ai-bricks.service 2>/dev/null || true
sudo systemctl mask cm4-ai-bricks.service 2>/dev/null || true
```

If still stuck, reboot:

```bash
sudo reboot
```

## DNS lookup fails but ping works

Test manually:

```bash
dig @192.168.1.207 google.com +short
dig @192.168.1.206 google.com +short
```

Check AdGuard or DNS service on each server.

## Port 53 fails

Test:

```bash
nc -vz 192.168.1.207 53
nc -vz 192.168.1.206 53
```

Install netcat if needed:

```bash
sudo apt install -y netcat-openbsd
```

DNS may still work over UDP even if TCP 53 fails, but a proper resolver should usually allow TCP too.

## AdGuard stats do not show

Check credentials:

```bash
sudo nano /etc/default/dual-dns-lcd
```

Set:

```text
ADGUARD_USER=admin
ADGUARD_PASSWORD=your_password_here
```

Restart:

```bash
sudo systemctl restart dual-dns-lcd
```

## Update screen does not appear

Test it:

```bash
echo "TEST UPDATE SCREEN" | sudo tee /run/cm4-safe-update.status
sudo systemctl stop dual-dns-lcd
sudo systemctl start cm4-update-lcd
```

Return to DNS:

```bash
sudo systemctl stop cm4-update-lcd
sudo systemctl restart dual-dns-lcd
```

## Nightly updater did not run

Check timer:

```bash
systemctl list-timers cm4-safe-nightly-update.timer --no-pager
```

Check log:

```bash
sudo tail -120 /var/log/cm4-safe-nightly-update.log
```

## System started updating when manually tested

This command really starts the updater:

```bash
sudo systemctl start cm4-safe-nightly-update.service
```

It will update and reboot. Use the test script if you only want to test the display:

```bash
sudo ./scripts/test_update_lcd.sh
```
