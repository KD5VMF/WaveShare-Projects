#!/usr/bin/env bash
set -euo pipefail

echo "=== Installing CM4 Bookworm-only safe updater ==="

BACKUP_DIR="/root/apt-bookworm-lock-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

cp -a /etc/apt/sources.list "$BACKUP_DIR/" 2>/dev/null || true
cp -a /etc/apt/sources.list.d "$BACKUP_DIR/" 2>/dev/null || true

echo "=== forcing apt sources to bookworm, not stable/testing/trixie ==="

for f in /etc/apt/sources.list /etc/apt/sources.list.d/*.list; do
  [ -f "$f" ] || continue
  sed -i \
    -e 's/[[:space:]]stable[[:space:]]/ bookworm /g' \
    -e 's/[[:space:]]testing[[:space:]]/ bookworm /g' \
    -e 's/[[:space:]]trixie[[:space:]]/ bookworm /g' \
    "$f"
done

for f in /etc/apt/sources.list.d/*.sources; do
  [ -f "$f" ] || continue
  sed -i \
    -e 's/^Suites:.*stable.*/Suites: bookworm/g' \
    -e 's/^Suites:.*testing.*/Suites: bookworm/g' \
    -e 's/^Suites:.*trixie.*/Suites: bookworm/g' \
    "$f"
done

cat > /etc/apt/preferences.d/00-stay-on-bookworm <<'EOF'
Package: *
Pin: release n=bookworm
Pin-Priority: 1001

Package: *
Pin: release n=bookworm-security
Pin-Priority: 1001

Package: *
Pin: release n=trixie
Pin-Priority: -1

Package: *
Pin: release n=testing
Pin-Priority: -1

Package: *
Pin: release a=testing
Pin-Priority: -1

Package: *
Pin: release a=stable
Pin-Priority: -1
EOF

mkdir -p /etc/update-manager
cat > /etc/update-manager/release-upgrades <<'EOF'
[DEFAULT]
Prompt=never
EOF

apt update
apt install -y needrestart apt-listchanges

cat > /usr/local/sbin/cm4-safe-nightly-update <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

LOG="/var/log/cm4-safe-nightly-update.log"
LOCK="/var/lock/cm4-safe-nightly-update.lock"
STATUS="/run/cm4-safe-update.status"

exec >>"$LOG" 2>&1

write_status() {
  echo "$1" > "$STATUS"
  echo
  echo "=== $1 ==="
}

fail_status() {
  echo "FAILED - CHECK LOG" > "$STATUS"
  echo
  echo "=== FAILED - CHECK LOG ==="
  sync
}
trap fail_status ERR

echo "STARTING" > "$STATUS"

systemctl start cm4-update-lcd.service || true
sleep 1

echo
echo "============================================================"
echo "CM4 safe nightly update started: $(date)"
echo "============================================================"

if ! flock -n "$LOCK" true; then
  write_status "ANOTHER UPDATE RUNNING"
  echo "Another update is already running. Exiting."
  exit 0
fi

exec 9>"$LOCK"
flock -n 9 || exit 0

export DEBIAN_FRONTEND=noninteractive
export NEEDRESTART_MODE=a

write_status "RECOVERY CHECK"
dpkg --configure -a || true
apt-get -f install -y || true

write_status "CHECK BOOKWORM LOCK"
if grep -RIsE ' trixie | testing | stable ' /etc/apt/sources.list /etc/apt/sources.list.d 2>/dev/null; then
  write_status "ABORT NON-BOOKWORM"
  echo "WARNING: non-bookworm source word found. Aborting update for safety."
  exit 1
fi

write_status "APT UPDATE"
apt-get update \
  -o Acquire::Retries=3 \
  -o Acquire::http::Timeout=20 \
  -o Acquire::https::Timeout=20

write_status "APT UPGRADE"
apt-get upgrade -y \
  -o Dpkg::Options::="--force-confdef" \
  -o Dpkg::Options::="--force-confold" \
  -o Acquire::Retries=3

write_status "CONFIGURING"
dpkg --configure -a || true
apt-get -f install -y || true

write_status "SAFE CLEANUP"
echo "Not running autoremove automatically, to avoid removing driver dependencies."

write_status "SYNCING"
sync
sleep 3
sync

write_status "REBOOTING"
echo
echo "Nightly update finished: $(date)"
echo "Rebooting now so LCD/drivers/services restart cleanly."

sleep 3
systemctl reboot
EOF

chmod +x /usr/local/sbin/cm4-safe-nightly-update

install -m 0644 systemd/cm4-safe-nightly-update.service /etc/systemd/system/cm4-safe-nightly-update.service
install -m 0644 systemd/cm4-safe-nightly-update.timer /etc/systemd/system/cm4-safe-nightly-update.timer

systemctl disable --now apt-daily.timer apt-daily-upgrade.timer 2>/dev/null || true
systemctl daemon-reload
systemctl enable --now cm4-safe-nightly-update.timer

echo "Safe updater installed."
echo "Backup saved at: $BACKUP_DIR"
systemctl list-timers cm4-safe-nightly-update.timer --no-pager
