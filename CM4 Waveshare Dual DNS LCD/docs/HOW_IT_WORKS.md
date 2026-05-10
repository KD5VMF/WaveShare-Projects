# How It Works

This project has three main parts:

1. Dual DNS LCD display
2. Update LCD display
3. Safe Bookworm-only nightly updater

## 1. Dual DNS LCD display

The main app is:

```text
/opt/dual-dns-lcd/dual_dns_lcd.py
```

It runs as:

```text
dual-dns-lcd.service
```

The app reads settings from:

```text
/etc/default/dual-dns-lcd
```

It checks each DNS server using simple network tests.

### Ping test

The app runs:

```bash
ping -c 1 -W 1 <server>
```

It extracts the response time and displays it.

### Port 53 test

The app opens a TCP socket to port 53:

```python
socket.create_connection((host, 53), timeout=1.0)
```

This checks whether something is listening on TCP/53. DNS is often UDP, but TCP/53 should normally be available on a proper DNS server too.

### Real DNS lookup

The app runs:

```bash
dig @<server> google.com +short +time=1 +tries=1
```

If it gets an answer, the server is resolving names.

### AdGuard Home stats

If credentials are configured, the app tries:

```text
/control/status
/control/stats
```

It first tries the base URL:

```text
http://<server>
```

then:

```text
http://<server>:3000
```

That covers common AdGuard Home setups.

If API access is unavailable, the screen does not show "API unavailable" text. It instead shows DNS answer, score, and lookup information.

---

## 2. Update LCD display

The update display is:

```text
/opt/cm4-update-lcd/update_lcd.py
```

It runs as:

```text
cm4-update-lcd.service
```

It reads:

```text
/run/cm4-safe-update.status
```

and tails:

```text
/var/log/cm4-safe-nightly-update.log
```

The updater writes short status messages, and the LCD refreshes them.

This is simple and robust because the update script does not have to draw graphics itself. It only writes a one-line status file.

---

## 3. Safe nightly updater

The updater script is:

```text
/usr/local/sbin/cm4-safe-nightly-update
```

It runs as:

```text
cm4-safe-nightly-update.service
```

and is scheduled by:

```text
cm4-safe-nightly-update.timer
```

The timer runs at night, updates the system, syncs disks, and reboots.

The updater is intentionally conservative:

```text
apt-get update
apt-get upgrade
```

It does not run:

```text
apt-get dist-upgrade
apt full-upgrade
apt autoremove
```

That avoids major release changes and avoids accidentally removing driver-related packages.

---

## Why the update screen takes over the LCD

Before the updater starts apt work, it runs:

```bash
systemctl start cm4-update-lcd.service
```

That service has conflicts with the normal DNS display, so systemd stops the DNS LCD and starts the update LCD.

After the update completes, the system reboots. On boot, the normal DNS LCD starts again because it is enabled.

---

## Why Bookworm is pinned

The CM4 LCD driver stack may depend on working GPIO/SPI/Python behavior in the current OS release. A major OS jump can change kernel, GPIO libraries, Python packages, or device tree behavior.

The project pins apt to Bookworm with:

```text
/etc/apt/preferences.d/00-stay-on-bookworm
```

and makes sure apt source files use `bookworm`, not `stable`, `testing`, or `trixie`.

This still allows normal Bookworm package updates.
