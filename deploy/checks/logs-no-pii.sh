#!/usr/bin/env bash
# deploy/checks/logs-no-pii.sh [staging] - OPS-CHK05: run after the full E2E suite against staging.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
log="$(mktemp)"
trap 'rm -f "$log"' EXIT
"$here/../compose.sh" "${1:-staging}" logs api --since 2h > "$log"
if grep -E '(\+?880|0)1[3-9][0-9]{8}|[০-৯]{11}' "$log"; then echo "Phone number found in logs"; exit 1; fi
while read -r addr; do
  [ -n "$addr" ] || continue
  if grep -qF "$addr" "$log"; then echo "Address found in logs"; exit 1; fi
done < "$here/../seed/addresses.txt"
echo "OPS-CHK05 passed"
