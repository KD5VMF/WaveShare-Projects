# GitHub upload notes

Suggested repository name:

```text
CM4-Waveshare-Dual-DNS-LCD
```

Before committing, make sure you do not include real files from `/etc/default` that contain passwords.

Safe to commit:

```text
defaults/*.example
src/*.py
systemd/*.service
systemd/*.timer
scripts/*.sh
README.md
docs/*.md
LICENSE
```

Do not commit:

```text
/etc/default/dual-dns-lcd
/etc/default/adguard-lcd
any pfSense backup XML
any SSH keys
any real password or token
```
