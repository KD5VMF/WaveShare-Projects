# CM4 Waveshare Dual DNS LCD + Safe Bookworm Updater

A Raspberry Pi Compute Module 4 / Waveshare LCD status display for watching two DNS / AdGuard Home servers, plus a safe Bookworm-only nightly updater that shows update progress on the LCD before rebooting.

This project was built for a CM4-based NAS / mini display system using the Waveshare 2-inch SPI LCD driver. It is designed to be useful on a headless Raspberry Pi OS Bookworm install.

The display app watches two DNS servers:

```text
DNS1 = 192.168.1.207
DNS2 = 192.168.1.206
```

It shows a high-contrast status screen with ping, TCP port 53, real DNS lookup, and optional AdGuard Home stats. During nightly updates, the display switches to an update progress screen so you can see what is happening before the system reboots.

---

## What this project does

### Dual DNS LCD screen

The main display shows two side-by-side DNS server panels.

Each server panel checks:

- ICMP ping
- TCP port 53 connectivity
- Real DNS lookup using `dig`
- Optional AdGuard Home API status
- Optional AdGuard protection state
- Optional AdGuard query count
- Optional AdGuard blocked query count
- Optional AdGuard block percentage
- Optional AdGuard average processing time

The screen uses bright, high-contrast colors:

- Green = healthy
- Yellow = warning
- Red = fail
- Bright blue/yellow/white text for readability

### Update progress LCD screen

When the safe updater runs, the LCD changes from the DNS dashboard to an update dashboard.

It shows phases such as:

```text
RECOVERY CHECK
CHECK BOOKWORM LOCK
APT UPDATE
APT UPGRADE
CONFIGURING
SAFE CLEANUP
SYNCING
REBOOTING
FAILED - CHECK LOG
```

It also tails the update log on-screen.

### Safe Bookworm-only updater

The updater is meant for systems where drivers currently work on Raspberry Pi OS Bookworm and you do **not** want the machine accidentally moving to a newer Debian/Raspberry Pi OS release.

It does this by:

- Pinning apt to Bookworm
- Rejecting Trixie/testing release sources
- Avoiding `dist-upgrade` and `full-upgrade`
- Avoiding automatic `autoremove`
- Running normal Bookworm package upgrades
- Recovering interrupted apt/dpkg work
- Syncing disks before reboot
- Rebooting after the nightly update

---

## Hardware used / expected

This was designed around:

- Raspberry Pi Compute Module 4
- CM4 NAS / Double Deck style carrier board
- Waveshare 2-inch SPI LCD, commonly driven by `LCD_2inch.py`
- Raspberry Pi OS Bookworm
- Two DNS / AdGuard Home servers on the LAN

Default IPs:

```text
DNS1: 192.168.1.207
DNS2: 192.168.1.206
```

Default Waveshare driver path:

```text
/home/sysop/CM4-NAS-Double-Deck_Demo/RaspberryPi
```

The Python programs import the display driver like this:

```python
sys.path.insert(0, LCD_LIB_DIR)
from lib import LCD_2inch
```

So your driver folder must contain:

```text
/home/sysop/CM4-NAS-Double-Deck_Demo/RaspberryPi/lib/LCD_2inch.py
```

If your path is different, edit the default files after installation.

---

## Software requirements

Recommended:

- Raspberry Pi OS Bookworm
- Python 3
- SPI enabled
- Working Waveshare LCD Python driver
- `python3-pil`
- `python3-spidev`
- `python3-numpy`
- `python3-rpi.gpio`
- `iputils-ping`
- `dnsutils`
- `systemd`

Install script handles the package install.

---

## Important Bookworm note

This project intentionally includes a Bookworm-locking safe update setup. That is because some LCD / GPIO / SPI drivers may work correctly on the current Bookworm stack but break after a major OS release jump.

The safe updater allows normal Bookworm upgrades while preventing accidental release migration.

It does **not** automatically run:

```text
apt full-upgrade
apt dist-upgrade
apt autoremove
```

That is deliberate.

---

## Folder layout

```text
CM4_Waveshare_Dual_DNS_LCD_GitHub_Ready/
├── README.md
├── LICENSE
├── src/
│   ├── dual_dns_lcd.py
│   └── update_lcd.py
├── defaults/
│   ├── dual-dns-lcd.example
│   └── cm4-update-lcd.example
├── systemd/
│   ├── dual-dns-lcd.service
│   ├── cm4-update-lcd.service
│   ├── cm4-safe-nightly-update.service
│   └── cm4-safe-nightly-update.timer
├── scripts/
│   ├── install.sh
│   ├── install_bookworm_safe_updates.sh
│   ├── set_dual_dns_boot.sh
│   ├── switch_to_dual_dns.sh
│   ├── test_update_lcd.sh
│   └── run_update_now.sh
└── docs/
    ├── HOW_IT_WORKS.md
    └── TROUBLESHOOTING.md
```

---

## Quick install

Clone or copy this project to the CM4, then run:

```bash
cd CM4_Waveshare_Dual_DNS_LCD_GitHub_Ready
chmod +x scripts/*.sh
sudo ./scripts/install.sh
```

That installs:

- `/opt/dual-dns-lcd/dual_dns_lcd.py`
- `/opt/cm4-update-lcd/update_lcd.py`
- `/etc/default/dual-dns-lcd`
- `/etc/default/cm4-update-lcd`
- systemd services

Start the DNS screen:

```bash
sudo systemctl restart dual-dns-lcd
sudo systemctl status dual-dns-lcd --no-pager -l
```

Make it start on boot:

```bash
sudo ./scripts/set_dual_dns_boot.sh
```

---

## Configure DNS server IPs

Edit:

```bash
sudo nano /etc/default/dual-dns-lcd
```

Default:

```text
DNS1_HOST=192.168.1.207
DNS2_HOST=192.168.1.206
DNS1_NAME=DNS1
DNS2_NAME=DNS2
DNS_TEST_NAME=google.com
```

Restart after changes:

```bash
sudo systemctl restart dual-dns-lcd
```

---

## Optional AdGuard Home stats

If you want query/block/protection stats, put your AdGuard Home web login in:

```bash
sudo nano /etc/default/dual-dns-lcd
```

Example:

```text
ADGUARD_USER=admin
ADGUARD_PASSWORD=your_password_here
```

The file is installed with mode `600` so only root can read it.

The app tries these per DNS server:

```text
http://<DNS_IP>/control/status
http://<DNS_IP>/control/stats
http://<DNS_IP>:3000/control/status
http://<DNS_IP>:3000/control/stats
```

If the API is not configured or unavailable, the display does **not** show API wording. It falls back to useful DNS data: answer, score, and lookup status.

---

## Safe nightly Bookworm updates

Install the safe updater:

```bash
sudo ./scripts/install_bookworm_safe_updates.sh
```

This installs:

- `/usr/local/sbin/cm4-safe-nightly-update`
- `/etc/systemd/system/cm4-safe-nightly-update.service`
- `/etc/systemd/system/cm4-safe-nightly-update.timer`
- `/etc/apt/preferences.d/00-stay-on-bookworm`

It also disables the built-in apt daily timers so only this controlled updater runs.

The timer runs at night:

```text
03:20 with up to 20 minutes randomized delay
```

Check schedule:

```bash
systemctl list-timers cm4-safe-nightly-update.timer --no-pager
```

View logs:

```bash
sudo tail -120 /var/log/cm4-safe-nightly-update.log
```

---

## Manual update run

To start an update manually:

```bash
sudo ./scripts/run_update_now.sh
```

or directly:

```bash
sudo systemctl start cm4-safe-nightly-update.service
```

When this runs, the LCD should automatically switch to the update screen.

The unit will update, sync disks, and reboot.

---

## Testing the update LCD without updating

```bash
sudo ./scripts/test_update_lcd.sh
```

Then return to DNS display:

```bash
sudo ./scripts/switch_to_dual_dns.sh
```

---

## Why only one LCD app can run

The Waveshare LCD driver owns GPIO/SPI pins. Only one program can control the LCD at a time.

If another old display program is still running, you may see errors like:

```text
lgpio.error: 'GPIO busy'
```

The systemd services in this project stop known conflicting LCD apps before starting.

Known example conflict:

```text
cm4-ai-bricks.service
```

The boot script masks it by default if present.

---

## Useful commands

Start DNS LCD:

```bash
sudo systemctl restart dual-dns-lcd
```

Stop DNS LCD:

```bash
sudo systemctl stop dual-dns-lcd
```

View DNS LCD logs:

```bash
journalctl -u dual-dns-lcd -n 100 --no-pager -l
```

Start update LCD manually:

```bash
echo "TEST UPDATE SCREEN" | sudo tee /run/cm4-safe-update.status
sudo systemctl start cm4-update-lcd
```

Return to DNS LCD:

```bash
sudo systemctl stop cm4-update-lcd
sudo systemctl restart dual-dns-lcd
```

Check Bookworm lock:

```bash
grep -RIsE 'bookworm|trixie|testing|stable' \
  /etc/apt/sources.list \
  /etc/apt/sources.list.d \
  /etc/apt/preferences.d/00-stay-on-bookworm
```

---

## Code overview

### `src/dual_dns_lcd.py`

Main DNS monitor display.

Important functions:

- `check_dns(name, host)`  
  Runs all checks for one DNS server.

- `ping_ms(host)`  
  Tests ICMP ping and extracts latency.

- `tcp_port_open(host, port=53)`  
  Tests TCP port 53.

- `dig_test(host)`  
  Runs a real DNS lookup with `dig @host google.com`.

- `get_adguard_stats(host)`  
  Tries the AdGuard Home API.

- `panel(draw, x, y, w, h, d)`  
  Draws one DNS server box.

- `draw_main(d1, d2)`  
  Draws the whole screen.

### `src/update_lcd.py`

Update progress display.

Important functions:

- `read_status()`  
  Reads `/run/cm4-safe-update.status`.

- `tail_log()`  
  Reads recent lines from `/var/log/cm4-safe-nightly-update.log`.

- `draw_screen()`  
  Draws the update phase, animated progress bar, and recent activity.

### `/usr/local/sbin/cm4-safe-nightly-update`

Nightly update script.

Important phases:

- Recovery check
- Bookworm lock check
- `apt-get update`
- `apt-get upgrade`
- dpkg configure
- safe cleanup
- sync
- reboot

It writes display status messages to:

```text
/run/cm4-safe-update.status
```

---

## Security notes

Do not commit real secrets to GitHub.

These files may contain secrets after you edit them:

```text
/etc/default/dual-dns-lcd
/etc/default/adguard-lcd
```

This repository only includes example/default files with `CHANGE_ME` placeholders.

---

## License

MIT
