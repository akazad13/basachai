#!/usr/bin/env bash
# Monthly (cron, as root): deploy/patch.sh [production]
# Pulls patched PostgreSQL 17 and Caddy images and restarts them (a few seconds of downtime), then removes old
# images. The API image is patched by its own monthly CI build and release.
set -euo pipefail
. "$(dirname "$0")/lib.sh"
env="${1:-production}"
lock "$env"
compose "$env" pull postgres caddy
compose "$env" up -d postgres caddy
wait_healthy "$env" postgres 120 || die "PostgreSQL is not healthy after the update"
wait_healthy "$env" api 180 || die "The API is not healthy after the PostgreSQL update"
docker image prune -f >/dev/null
echo "Patched $env"
