#!/usr/bin/env bash
# Restores a backup onto this server (OPS-2), replacing the database and the key ring:
#   deploy/restore.sh <environment> latest|<db-backup.gpg> [<keys-backup.gpg>] --yes
# Before running it on a fresh server: setup-server.sh, CONFIG_DIR filled in again (it is not in the backups),
# API_IMAGE in CONFIG_DIR/release.env, and the backup private key imported into root's gpg keyring
# (`gpg --import`; delete it again afterwards). With BACKUP_REMOTE set, backups are fetched first.
set -euo pipefail
. "$(dirname "$0")/lib.sh"
env="${1:?usage: restore.sh <environment> latest|<db-backup.gpg> [<keys-backup.gpg>] --yes}"
which="${2:?usage: restore.sh <environment> latest|<db-backup.gpg> [<keys-backup.gpg>] --yes}"
keys="${3:-}"
[ "${!#}" = "--yes" ] || die "This replaces the $env database and key ring. Add --yes to go ahead."
[ "$keys" = "--yes" ] && keys=""
dir="$(config_dir "$env")"
. "$dir/backup.env"
db="$(env_value "$env" DB_NAME)"
lock "$env"

image="$(sed -n 's/^API_IMAGE=//p' "$dir/release.env" 2>/dev/null || true)"
[ -n "$image" ] || die "Set API_IMAGE=<image> in $dir/release.env first (the image to run after the restore)."
docker image inspect "$image" >/dev/null 2>&1 || docker pull "$image"

if [ "$which" = latest ]; then
  if [ -n "${BACKUP_REMOTE:-}" ]; then mkdir -p "$BACKUP_DIR"; rclone copy "$BACKUP_REMOTE" "$BACKUP_DIR" --include '*.gpg'; fi
  which="$(find "$BACKUP_DIR" -maxdepth 1 -name 'db-*.dump.gpg' -printf '%f\n' 2>/dev/null | sort -r | head -n 1)"
  [ -n "$which" ] || die "No backups in $BACKUP_DIR"
  which="$BACKUP_DIR/$which"
fi
# The key ring taken in the same run as the database dump.
if [ -z "$keys" ]; then keys="$(dirname "$which")/$(basename "$which" | sed 's/^db-/keys-/; s/\.dump\.gpg$/.tar.gpg/')"; fi
[ -f "$which" ] || die "Not found: $which"
[ -f "$keys" ] || die "Not found: $keys (the key ring is needed to decrypt addresses)"
echo "Restoring $env from $(basename "$which") and $(basename "$keys")"

# Decrypt once, before anything is dropped. Not --batch, so gpg can ask for the key's passphrase.
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
gpg --quiet --output "$work/db.dump" --decrypt "$which"
gpg --quiet --output "$work/keys.tar" --decrypt "$keys"

compose "$env" stop api 2>/dev/null || true
compose "$env" up -d postgres
wait_healthy "$env" postgres 120 || die "PostgreSQL did not start"

compose "$env" exec -T postgres psql -U postgres -v ON_ERROR_STOP=1 -d postgres \
  -c "DROP DATABASE IF EXISTS \"$db\" WITH (FORCE)" \
  -c "CREATE DATABASE \"$db\" OWNER basachai_migrator" \
  -c "REVOKE ALL ON DATABASE \"$db\" FROM PUBLIC" \
  -c "GRANT CONNECT ON DATABASE \"$db\" TO basachai_api"
compose "$env" exec -T postgres psql -U postgres -v ON_ERROR_STOP=1 -d "$db" \
  -c "ALTER SCHEMA public OWNER TO basachai_migrator" -c "REVOKE CREATE ON SCHEMA public FROM PUBLIC"
compose "$env" exec -T postgres pg_restore -U basachai_migrator -d "$db" --no-owner --no-privileges --exit-on-error < "$work/db.dump"

compose "$env" run --rm --no-deps -T --user root --entrypoint sh api \
  -c 'find /keys -mindepth 1 -delete && tar -C /keys -xf - && chown -R app:app /keys' < "$work/keys.tar"

compose "$env" run --rm migrate
compose "$env" up -d
wait_healthy "$env" api 180 || die "The API did not become healthy after the restore; check: deploy/compose.sh $env logs api"
echo "Restored $env. Check that a stored address shows on an owner's edit screen (OPS-CHK02), then remove the private key."
