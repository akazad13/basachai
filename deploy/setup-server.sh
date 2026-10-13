#!/usr/bin/env bash
# One-time setup of a fresh Ubuntu 24.04 VPS, as root: sudo bash /opt/basachai/deploy/setup-server.sh <admin-user> [<deploy-user>]
# <admin-user> must already exist with an SSH key in ~/.ssh/authorized_keys: password and root logins are turned off.
# <deploy-user> (default: the admin user) is the account CI connects as; it may sudo only basachai-release.
# Then copy deploy/config-examples into /etc/basachai (and /etc/basachai-staging), fill them in, add the Cloudflare
# origin certificate as /etc/basachai/certs/origin.pem and origin.key, and deploy with deploy.sh.
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "Run as root" >&2; exit 1; }
admin="${1:?usage: setup-server.sh <admin-user> [<deploy-user>]}"
deployer="${2:-$admin}"
id "$deployer" >/dev/null 2>&1 || { echo "No user $deployer" >&2; exit 1; }
home="$(getent passwd "$admin" | cut -d: -f6)"
[ -s "$home/.ssh/authorized_keys" ] || { echo "$admin has no SSH key; add one before turning off password logins" >&2; exit 1; }
here="$(cd "$(dirname "$0")" && pwd)"

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y docker.io docker-compose-v2 ufw fail2ban python3-systemd unattended-upgrades gnupg rclone curl

# SSH: keys only, no root.
cat > /etc/ssh/sshd_config.d/10-basachai.conf <<'CONF'
PasswordAuthentication no
KbdInteractiveAuthentication no
PermitRootLogin no
CONF
sshd -t
systemctl reload ssh

# fail2ban reads SSH failures from the systemd journal (Ubuntu 24.04 images may have no auth.log).
cat > /etc/fail2ban/jail.d/basachai.conf <<'CONF'
[sshd]
enabled = true
backend = systemd
CONF
systemctl enable fail2ban
systemctl restart fail2ban

# Security updates install themselves.
cat > /etc/apt/apt.conf.d/20auto-upgrades <<'CONF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
CONF

# 4 GB swap (architecture spec, Server layout).
if ! swapon --show | grep -q .; then
  fallocate -l 4G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
  grep -q '^/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

# Docker log rotation for every container: 10 MB, 5 files.
cat > /etc/docker/daemon.json <<'CONF'
{ "log-driver": "json-file", "log-opts": { "max-size": "10m", "max-file": "5" } }
CONF
systemctl enable docker
systemctl restart docker

# Secrets are root-only (security spec); the admin user runs the deploy scripts with sudo.
install -d -m 700 -o root -g root /etc/basachai /etc/basachai/certs /etc/basachai-staging /etc/basachai-staging/certs /var/backups/basachai
usermod -aG sudo "$admin"

# The deploy entry point, root-owned, and the only command the deploy user may run as root.
chmod 755 "$here"/*.sh "$here"/checks/*.sh "$here"/postgres/*.sh
install -m 755 -o root -g root "$here/server-release.sh" /usr/local/sbin/basachai-release
echo "$deployer ALL=(root) NOPASSWD: /usr/local/sbin/basachai-release" > /etc/sudoers.d/basachai
chmod 440 /etc/sudoers.d/basachai
visudo -cf /etc/sudoers.d/basachai

"$here/firewall.sh"

cat > /etc/cron.d/basachai <<'CONF'
# cron's default PATH has no sbin, where ufw and friends live.
PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
# Nightly encrypted backup, a weekly refresh of Cloudflare's address ranges, and monthly PostgreSQL and Caddy patches.
30 2 * * * root bash /opt/basachai/deploy/backup.sh production >> /var/log/basachai-backup.log 2>&1
0 4 * * 1 root bash /opt/basachai/deploy/firewall.sh >> /var/log/basachai-firewall.log 2>&1
0 5 1 * * root bash /opt/basachai/deploy/patch.sh production >> /var/log/basachai-patch.log 2>&1
CONF
echo "Server ready. Next (deploy/README.md): fill in /etc/basachai, log Docker in to the registry, then release from CI."
