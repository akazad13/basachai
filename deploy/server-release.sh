#!/usr/bin/env bash
# Installed by setup-server.sh as /usr/local/sbin/basachai-release: the one command the deploy user may run with
# sudo. Reads a tar of deploy/ on stdin, installs it root-owned in /opt/basachai/deploy, then runs release.sh.
set -euo pipefail
env="${1:?usage: basachai-release <production|staging> <image>}"
image="${2:?usage: basachai-release <production|staging> <image>}"
case "$env" in production|staging) ;; *) echo "Unknown environment: $env" >&2; exit 1 ;; esac
[[ "$image" =~ ^[a-z0-9][a-z0-9./_:@-]*$ ]] || { echo "Not an image name: $image" >&2; exit 1; }

incoming="$(mktemp -d)"
trap 'rm -rf "$incoming"' EXIT
tar -C "$incoming" --no-same-owner --no-same-permissions -xf -
[ -f "$incoming/deploy/release.sh" ] || { echo "The upload has no deploy/release.sh" >&2; exit 1; }

# Root-owned and not writable by anyone else: cron runs these scripts as root.
chown -R root:root "$incoming/deploy"
chmod -R u=rwX,go=rX "$incoming/deploy"
find "$incoming/deploy" -name '*.sh' -exec chmod 755 {} +
install -d -m 755 -o root -g root /opt/basachai
rm -rf /opt/basachai/deploy.previous
if [ -d /opt/basachai/deploy ]; then mv /opt/basachai/deploy /opt/basachai/deploy.previous; fi
mv "$incoming/deploy" /opt/basachai/deploy

exec /opt/basachai/deploy/release.sh "$env" "$image"
