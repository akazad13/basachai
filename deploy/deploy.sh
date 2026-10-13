#!/usr/bin/env bash
# From CI or a developer machine: deploy/deploy.sh <user@ssh-host> <production|staging> <image>
# Sends deploy/ to the server and runs basachai-release there (the only command the deploy user may sudo).
set -euo pipefail
host="${1:?usage: deploy.sh <user@ssh-host> <environment> <image>}"
env="${2:?usage: deploy.sh <user@ssh-host> <environment> <image>}"
image="${3:?usage: deploy.sh <user@ssh-host> <environment> <image>}"
root="$(cd "$(dirname "$0")/.." && pwd)"

tar -C "$root" --owner=0 --group=0 --exclude=deploy/local-config -cf - deploy \
  | ssh "$host" sudo /usr/local/sbin/basachai-release "$env" "$image"
