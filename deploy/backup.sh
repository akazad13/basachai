#!/usr/bin/env bash
# Nightly backup (cron, as root): deploy/backup.sh [production|staging]
# Encrypts a pg_dump and the Data Protection key ring (needed to decrypt addresses) to the backup public key,
# keeps 14 daily and 8 weekly copies here, and copies them off-site with rclone when BACKUP_REMOTE is set.
# Settings: CONFIG_DIR/backup.env (BACKUP_DIR, BACKUP_RECIPIENT_FILE, BACKUP_REMOTE, BACKUP_PING_URL).
set -euo pipefail
. "$(dirname "$0")/lib.sh"
env="${1:-production}"
dir="$(config_dir "$env")"
. "$dir/backup.env"
: "${BACKUP_DIR:?}" "${BACKUP_RECIPIENT_FILE:?}"
db="$(env_value "$env" DB_NAME)"
stamp="$(date -u +%Y%m%d-%H%M%S)"
umask 077
mkdir -p "$BACKUP_DIR"
lock "$env"

# A failed run tells the monitor, so a silent cron failure does not go unnoticed for days.
trap '[ -n "${BACKUP_PING_URL:-}" ] && curl -fsS -m 10 "$BACKUP_PING_URL/fail" >/dev/null || true' ERR

encrypt() { gpg --batch --yes --quiet --trust-model always --recipient-file "$BACKUP_RECIPIENT_FILE" --encrypt; }

# Written under a temporary name and renamed only when complete, so a failed run never looks like a backup.
db_file="$BACKUP_DIR/db-$stamp.daily.dump.gpg"
# Without the hash salts: an old backup must not bring back a salt the live database has deleted. A restore starts
# a new salt, which only means hashes from before and after it are not comparable.
compose "$env" exec -T postgres pg_dump -U basachai_migrator -d "$db" -Fc --exclude-table-data=hash_salts | encrypt > "$db_file.part"
mv "$db_file.part" "$db_file"

keys_file="$BACKUP_DIR/keys-$stamp.daily.tar.gpg"
compose "$env" run --rm --no-deps -T --entrypoint tar api -C /keys -cf - . | encrypt > "$keys_file.part"
mv "$keys_file.part" "$keys_file"

# Sundays also keep a weekly copy.
if [ "$(date -u +%u)" = 7 ] || [ "${FORCE_WEEKLY:-0}" = 1 ]; then
  cp "$db_file" "${db_file/.daily./.weekly.}"
  cp "$keys_file" "${keys_file/.daily./.weekly.}"
fi

# Keeps the newest $2 files matching $1. Names start with a sortable UTC time, so name order is age order.
prune() {
  find "$BACKUP_DIR" -maxdepth 1 -name "$1" -printf '%f\n' | sort -r | tail -n +"$(($2 + 1))" \
    | while read -r f; do rm -f -- "$BACKUP_DIR/$f"; done
}
prune 'db-*.daily.dump.gpg' 14
prune 'keys-*.daily.tar.gpg' 14
prune 'db-*.weekly.dump.gpg' 8
prune 'keys-*.weekly.tar.gpg' 8

# copy, not sync: losing or emptying this disk must never delete the off-site copies. Expire them with the
# storage's own retention (for example a bucket lifecycle rule), using credentials that cannot delete.
if [ -n "${BACKUP_REMOTE:-}" ]; then
  rclone copy "$BACKUP_DIR" "$BACKUP_REMOTE" --include '*.gpg'
fi
if [ -n "${BACKUP_PING_URL:-}" ]; then curl -fsS -m 10 "$BACKUP_PING_URL" >/dev/null || true; fi
echo "Backed up $env: $(basename "$db_file"), $(basename "$keys_file")"
