#!/usr/bin/env bash
# deploy/checks/rollback-drill.sh [staging] - OPS-CHK04 (never production: it takes the API down for minutes)
# Releases an image that starts but never passes its health check, then rolls back without a database restore.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
. "$here/../lib.sh"
env="${1:-staging}"
case "$env" in staging|local) ;; *) die "Run the drill on staging (or local), not $env." ;; esac
dir="$(config_dir "$env")"
good="$(image_in "$dir/release.env")"
[ -n "$good" ] || die "Release a working image to $env first."
saved_previous="$(image_in "$dir/release.previous.env")"

# Same image, but listening on a port the health check does not use; the migrate step still works.
printf 'FROM %s\nENV ASPNETCORE_HTTP_PORTS=9999\n' "$good" | docker build -q -t basachai-api:broken - >/dev/null
if PULL=0 "$here/../release.sh" "$env" basachai-api:broken; then die "The broken image passed its health check"; fi
"$here/../rollback.sh" "$env"
[ "$(image_in "$dir/release.env")" = "$good" ] || die "release.env does not name $good"
compose "$env" exec -T api curl -fsS http://localhost:8080/v1/locations > /dev/null

# Leave the rollback target as it was, not pointing at the drill's image.
if [ -n "$saved_previous" ]; then echo "API_IMAGE=$saved_previous" > "$dir/release.previous.env"; else rm -f "$dir/release.previous.env"; fi
docker image rm basachai-api:broken >/dev/null 2>&1 || true
echo "OPS-CHK04 passed: $good serves again"
