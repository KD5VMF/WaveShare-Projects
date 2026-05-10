# AGWatch REV16 Auto DNS Save

**AGWatch REV16 Auto DNS Save** is a Windows desktop dashboard for watching **two AdGuard Home DNS servers** from one screen.

This revision keeps the REV14 dual-DNS monitoring layout and adds separate enable checkboxes for DNS1 and DNS2. You can temporarily disable either DNS box from the dashboard to confirm that each server is actually answering and logging queries. AGWatch will not let both boxes be disabled at the same time.

Default home-lab targets in this build:

```text
DNS1 mini: http://192.168.1.206:80
DNS2 nuc1: http://192.168.1.207:3000
```

You can change both URL fields inside the app. The second value includes `:3000` on purpose so it works with an AdGuard admin web interface running on port 3000.

---

## What it looks at

AGWatch connects to each enabled AdGuard Home web/API address using your AdGuard username and password, then polls:

- `/control/status`
- `/control/stats`
- `/control/querylog`

It does **not** change AdGuard settings. It is a read-only monitoring dashboard.

---

## Main features

- Watches **two AdGuard Home servers** at once.
- Separate URL/port boxes for DNS1 and DNS2.
- DNS1/DNS2 URL changes now update the dashboard cards/source names automatically.
- DNS1/DNS2 URL, user, password, poll, row count, auto-start, and checkbox changes are saved as you edit them.
- Fixed the cramped **Rows** label in the top bar so the `s` no longer drops below the word.
- New **Use DNS1** checkbox.
- New **Use DNS2** checkbox.
- Prevents DNS1 and DNS2 from both being unchecked at the same time.
- Lets you test one DNS server at a time from the same dashboard.
- Disabled per-server card shows **DISABLED** instead of looking like a failed/offline server.
- Opens DNS1, DNS2, or both AdGuard web UIs.
- Combined total DNS query count.
- Combined blocked count.
- Combined overall block-rate card.
- Combined live query-per-second card.
- Per-server live cards:
  - `DNS1 MINI`
  - `DNS2 NUC1`
- `ACTIVE DNS NOW` card showing which enabled DNS server handled more live work during the current poll.
- Recent query log includes a **DNS BOX** column so you can see which computer handled each lookup.
- Combined top clients panel.
- Combined top blocked / filtered domains panel.
- Query-per-second history graph.
- Dynamic live block-rate history graph.
- Q/S and block/S meters.
- Starts maximized.
- One-button **FULLSCREEN / WINDOWED** toggle.
- Adjustable polling interval.
- Adjustable recent query log row count.
- Auto-start polling option.
- Saves settings under the current Windows user profile.
- Saves the AdGuard password encrypted with Windows DPAPI, not plain text.

---

## REV16 highlights

### Small top-bar fixes

REV16 fixes the cramped **Rows** label so it stays on one line.

When you edit the DNS1 or DNS2 URL/IP field, AGWatch now immediately:

- Saves the new address to `Documents\AGWatch\settings.json`.
- Updates the DNS1/DNS2 cards to show the new host/IP instead of the old default.
- Uses the new address for the next poll without restarting the program.
- Resets that DNS box's runtime counter cache so the next q/s calculation is not compared against the old server.

### DNS1 / DNS2 test toggles

REV16 adds two dashboard checkboxes:

```text
Use DNS1
Use DNS2
```

Use them to test each DNS box individually:

- Leave both checked for normal combined monitoring.
- Uncheck **Use DNS1** to watch only DNS2.
- Uncheck **Use DNS2** to watch only DNS1.
- If you try to uncheck both, AGWatch automatically turns the last one back on and shows a warning.

This makes it easier to confirm that each AdGuard computer is doing work and that the query log source column is correct.

### Disabled is different from offline

If a DNS server is unchecked, its card shows:

```text
DISABLED
```

That way you can tell the difference between a server you intentionally turned off in AGWatch and a server that failed to answer.

---

## REV14 features kept

### Dual DNS monitoring

AGWatch polls the configured AdGuard servers and merges their status into one control-room dashboard.

The query log table has this extra column:

```text
DNS BOX
```

Example values:

```text
DNS1 192.168.1.206
DNS2 192.168.1.207
```

That lets you see which physical DNS computer answered the query.

### Separate URL/port fields

The top bar has two server fields:

```text
DNS1 URL
DNS2 URL
```

Examples:

```text
http://192.168.1.206:80
http://192.168.1.207:3000
```

You can use port `80`, `3000`, or any other AdGuard web/API port by typing it into the URL.

### Active DNS card

The `ACTIVE DNS NOW` card shows the enabled DNS box with the highest live query rate during the current polling interval. If enabled servers are online but idle, it reports the idle/active state. If one enabled server is offline, it shows partial status.

---

## Password and settings safety

AGWatch saves settings here:

```text
Documents\AGWatch\settings.json
```

The AdGuard password is saved as `EncryptedPassword` using Windows DPAPI.

That means:

- The real password is not written to the settings file in plain text.
- The encrypted password is tied to your Windows user account.
- Another Windows account or another PC should not be able to decrypt it.
- If you move the settings file to another PC, re-enter the AdGuard password once.

Legacy plain-text `Password` values from older test builds are imported one time, then cleared when settings are saved again.

The DNS toggle settings are saved too:

```json
"PrimaryEnabled": true,
"SecondaryEnabled": true
```

---

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK for building from source
- One or two AdGuard Home servers reachable from the Windows PC
- AdGuard Home username and password

This project targets:

```text
net8.0-windows
Windows Forms
```

---

## Quick start

1. Download or clone this repository.
2. Open the folder in Windows.
3. Run:

```cmd
run.cmd
```

The script will:

1. Clean the Release build.
2. Build the app.
3. Start `AGWatch_REV16_AutoDnsSave.exe`.

When AGWatch opens, enter:

```text
DNS1 URL: http://192.168.1.206:80
DNS2 URL: http://192.168.1.207:3000
User:     your AdGuard username
Pass:     your AdGuard password
```

Then click **START**.

For normal monitoring, leave both **Use DNS1** and **Use DNS2** checked.

To test one server at a time:

```text
DNS1 only: Use DNS1 checked, Use DNS2 unchecked
DNS2 only: Use DNS1 unchecked, Use DNS2 checked
```

---

## Debug run

If the normal run does not start, use:

```cmd
run_debug.cmd
```

That creates these files in the project folder:

```text
build_log_REV16.txt
run_log_REV16.txt
```

The app can also write this crash log:

```text
Documents\AGWatchatal_error_REV16.txt
```

Those files are the best files to check when troubleshooting.

---

## Reset saved settings

To clear saved server/user/password/toggle settings, run:

```cmd
reset_settings.cmd
```

That backs up and deletes:

```text
Documents\AGWatch\settings.json
```

A backup is saved as:

```text
Documents\AGWatch\settings.json.backup
```

---

## Build manually

From the repository root:

```cmd
dotnet build AGWatch\AGWatch.csproj -c Release
```

The compiled EXE will be here:

```text
AGWatchin\Release
et8.0-windows\AGWatch_REV16_AutoDnsSave.exe
```

---

## Publish a local release build

A helper script is included:

```cmd
publish_win_x64.cmd
```

It publishes a local Windows x64 release build to:

```text
publish\win-x64
```

---

## Project layout

```text
AGWatch_REV16_AutoDnsSave/
├─ AGWatch/
│  ├─ AGWatch.csproj
│  ├─ Program.cs
│  ├─ MainForm.cs
│  ├─ DashboardControls.cs
│  ├─ AdGuardModels.cs
│  ├─ PasswordVault.cs
│  ├─ app.ico
│  └─ app.png
├─ run.cmd
├─ run_debug.cmd
├─ reset_settings.cmd
├─ publish_win_x64.cmd
├─ README.md
├─ CHANGELOG.md
├─ SECURITY.md
├─ VERSION.txt
└─ .gitignore
```

---

## Notes for GitHub

Recommended repository description:

```text
AGWatch REV16 Auto DNS Save — Windows dashboard for monitoring and testing two AdGuard Home DNS servers.
```

Suggested topics:

```text
adguard-home, dns, windows-forms, dotnet, homelab, monitoring, dashboard
```

Do not upload your personal `settings.json`, build output folders, debug logs, screenshots that show sensitive information, or pfSense backup XML files.

---

## Revision history summary

- **REV9** — Short-name AGWatch release with encrypted password saving.
- **REV10** — More useful block-rate and top blocked-domain panels.
- **REV11** — Dynamic live block-rate graph logic.
- **REV12** — Build fix, GitHub-ready cleanup, and final dynamic block-rate version.
- **REV14 DualDNS PortFix** — Adds two-server AdGuard monitoring, two URL/port fields, per-server live cards, combined totals, and DNS source tagging in the query log.
- **REV16 Auto DNS Save** — Adds Use DNS1 and Use DNS2 checkboxes so either server can be disabled for testing while preventing both from being disabled.

---

## License

No license file is included by default. Add the license you want before publishing publicly if you want others to have clear reuse rights.

---

## Web/API ports vs DNS port

The two URL boxes at the top of the app are for the AdGuard Home **web/API** ports:

- DNS1 mini web/API: `http://192.168.1.206:80`
- DNS2 nuc1 web/API: `http://192.168.1.207:3000`

Both DNS servers still answer real DNS filtering on port `53`. The app talks to the web/API port so it can read stats and query logs. Your client devices use port `53` through pfSense/DHCP.
