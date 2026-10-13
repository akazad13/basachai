#!/usr/bin/env bash
# docker compose for one environment: deploy/compose.sh <production|staging|local> <compose arguments...>
# Reads env/<environment>.env, then CONFIG_DIR/site.env (domains) and CONFIG_DIR/release.env (the API image).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
. "$here/lib.sh"
env="${1:?usage: compose.sh <production|staging|local> <compose arguments...>}"
shift
[ -f "$here/env/$env.env" ] || die "Unknown environment: $env"
dir="$(config_dir "$env")"
[ -d "$dir" ] || die "Missing $dir: copy deploy/config-examples there and fill it in."

# Caddy and every API share this network; Caddy has a fixed address on it (the API trusts its forwarded headers).
docker network inspect basachai-edge >/dev/null 2>&1 || docker network create --subnet 172.30.0.0/24 basachai-edge >/dev/null

# Git Bash on Windows (local runs only, with MSYS_NO_PATHCONV=1): give Docker native paths.
if command -v cygpath >/dev/null 2>&1; then here="$(cygpath -m "$here")"; dir="$(cygpath -m "$dir")"; fi

args=(--project-directory "$here" -f "$here/docker-compose.yml" --env-file "$here/env/$env.env")
for f in site.env release.env; do
  if [ -f "$dir/$f" ]; then args+=(--env-file "$dir/$f"); fi
done
# CONFIG_DIR as an absolute path, so relative paths in the settings file do not depend on where this runs.
CONFIG_DIR="$dir" exec docker compose "${args[@]}" "$@"
