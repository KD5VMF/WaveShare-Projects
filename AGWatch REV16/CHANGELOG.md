# Changelog

## REV16 Auto DNS Save

- Fixed the cramped top-bar **Rows** label so it stays on one line.
- Added live URL/IP auto-save for DNS1 and DNS2 edits.
- Updated DNS1/DNS2 card titles and source names to follow the current URL/IP fields instead of hard-coded mini/nuc1 labels.
- Reset per-server runtime counters when a DNS URL changes, preventing old-server counters from affecting the next q/s readout.
- Added **Use DNS1** checkbox.
- Kept **Use DNS2** checkbox and moved both toggles together on the top row.
- Prevented both DNS servers from being disabled at the same time.
- Saved the DNS1/DNS2 enabled state in `Documents\AGWatch\settings.json`.
- Changed disabled per-server cards to show `DISABLED` instead of looking like a broken/offline server.
- Updated app title, executable name, run scripts, debug logs, and README for REV16.
- Kept REV14 DualDNS PortFix behavior for web/API ports and combined statistics.

## REV14 DualDNS PortFix

- Clarifies that the top URL boxes are AdGuard web/API ports, not DNS port 53.
- Makes DNS1 explicit as `http://192.168.1.206:80` and DNS2 as `http://192.168.1.207:3000`.
- Keeps DNS filtering on port 53 for both AdGuard boxes.
- Moves large card values and meter values slightly upward for cleaner alignment.
- Keeps the dual-server query log that shows whether DNS1 mini or DNS2 nuc1 handled each row.

## REV14 DualDNS

- Added support for watching two AdGuard Home servers at the same time.
- Added separate `DNS1 URL` and `DNS2 URL` fields.
- Added support for different web/API ports per DNS server, such as `http://192.168.1.207:3000`.
- Added `Use DNS2` checkbox.
- Added `OPEN DNS1`, `OPEN DNS2`, and `OPEN BOTH` buttons.
- Added per-server dashboard cards for `DNS1 MINI` and `DNS2 NUC1`.
- Added `ACTIVE DNS NOW` card to show which DNS computer is handling more live DNS work during the current poll.
- Added `DNS BOX` column to the recent query log.
- Combined total DNS queries, blocked queries, block rate, query/sec, block/sec, top clients, and top blocked domains from both servers.
- Kept encrypted password storage using Windows DPAPI.

## REV12

- Build fix for local variable name conflict.
- GitHub-ready cleanup.
- Dynamic live block-rate history graph.

## REV11

- Added dynamic live block-rate graph logic.

## REV10

- Improved block-rate and blocked-domain panels.

## REV9

- Short-name AGWatch release with encrypted password saving.
