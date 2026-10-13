#!/usr/bin/env bash
# Runs on the server: deploy/release.sh <production|staging> <image>
# Pulls the image, applies migrations with it, then switches the API to it and waits for the health check.
# Migrations are expand-then-contract, so the last good image still works if this one fails (OPS-4).
#   release.env           the image the API runs
#   release.good.env      the last image that passed its health check
#   release.previous.env  where rollback.sh goes: the last good image before this release
set -euo pipefail
. "$(dirname "$0")/lib.sh"
env="${1:?usage: release.sh <environment> <image>}"
image="${2:?usage: release.sh <environment> <image>}"
dir="$(config_dir "$env")"
lock "$env"

# Caddy must not start with an empty origin secret (it would let other Cloudflare zones through).
if compose "$env" config --services | grep -qx caddy && [ "$(env_value "$env" ORIGIN_CHECK)" != off ]; then
  secret="$(sed -n 's/^ORIGIN_SECRET=//p' "$dir/caddy.env" 2>/dev/null || true)"
  [ "${#secret}" -ge 32 ] || die "Set ORIGIN_SECRET (32+ characters) in $dir/caddy.env, matching the Cloudflare Transform Rule."
fi

good="$(image_in "$dir/release.good.env")"
[ -n "$good" ] || good="$(image_in "$dir/release.env")"
if [ "${PULL:-1}" = 1 ]; then docker pull "$image"; fi

echo "Applying migrations with $image"
API_IMAGE="$image" compose "$env" run --rm migrate

if [ -n "$good" ] && [ "$good" != "$image" ]; then echo "API_IMAGE=$good" > "$dir/release.previous.env.tmp" && mv "$dir/release.previous.env.tmp" "$dir/release.previous.env"; fi
echo "API_IMAGE=$image" > "$dir/release.env.tmp" && mv "$dir/release.env.tmp" "$dir/release.env"
compose "$env" up -d --remove-orphans

if wait_healthy "$env" api 180; then
  echo "API_IMAGE=$image" > "$dir/release.good.env"
  docker image prune -f >/dev/null   # old, unused image layers
  echo "Released $image to $env"
else
  echo "The new API image failed its health check." >&2
  [ -n "$good" ] && [ "$good" != "$image" ] && echo "Restart the last good image ($good) with: deploy/rollback.sh $env" >&2
  exit 1
fi
