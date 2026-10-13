#!/usr/bin/env bash
# Restarts the last good image from before the latest release, without touching the database (OPS-4):
#   deploy/rollback.sh <production|staging>
set -euo pipefail
. "$(dirname "$0")/lib.sh"
env="${1:?usage: rollback.sh <environment>}"
dir="$(config_dir "$env")"
lock "$env"
previous="$(image_in "$dir/release.previous.env")"
[ -n "$previous" ] || die "No previous image recorded in $dir/release.previous.env"

echo "API_IMAGE=$previous" > "$dir/release.env.tmp" && mv "$dir/release.env.tmp" "$dir/release.env"
compose "$env" up -d api
wait_healthy "$env" api 180 || die "The previous image ($previous) is not healthy either; check: deploy/compose.sh $env logs api"
echo "API_IMAGE=$previous" > "$dir/release.good.env"
echo "Rolled back $env to $previous"
