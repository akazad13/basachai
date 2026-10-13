# shellcheck shell=bash
# Shared helpers for the deploy scripts. Source it: . "$(dirname "$0")/lib.sh"
DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

compose() { "$DEPLOY_DIR/compose.sh" "$@"; }

# The CONFIG_DIR of an environment, as an absolute path.
config_dir() {
  local dir
  dir="$(sed -n 's/^CONFIG_DIR=//p' "$DEPLOY_DIR/env/$1.env")"
  case "$dir" in /*) echo "$dir" ;; *) echo "$DEPLOY_DIR/${dir#./}" ;; esac
}

# A value from the environment's settings file.
env_value() { sed -n "s/^$2=//p" "$DEPLOY_DIR/env/$1.env"; }

# Waits for a service's container health check. Returns 1 if it turns unhealthy or does not pass in time.
wait_healthy() {
  local env="$1" service="$2" timeout="${3:-180}" id status waited=0
  while [ "$waited" -lt "$timeout" ]; do
    id="$(compose "$env" ps -q "$service")"
    status="$( [ -n "$id" ] && docker inspect -f '{{.State.Health.Status}}' "$id" 2>/dev/null || echo starting)"
    case "$status" in
      healthy) return 0 ;;
      unhealthy) return 1 ;;
    esac
    sleep 3
    waited=$((waited + 3))
  done
  return 1
}

die() { echo "$*" >&2; exit 1; }

# One deploy script at a time per environment (release, rollback, backup, restore); waits up to 10 minutes.
lock() {
  mkdir -p /run/lock
  exec 9>"/run/lock/basachai-$1.lock"
  flock -w 600 9 || die "Another deploy script is still running for $1"
}

# The image in a release file (release.env, release.good.env, release.previous.env), or nothing.
image_in() { sed -n 's/^API_IMAGE=//p' "$1" 2>/dev/null || true; }
