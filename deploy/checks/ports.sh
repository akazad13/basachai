#!/usr/bin/env bash
# deploy/checks/ports.sh <vps-ip> - OPS-CHK01 (run from a machine outside Cloudflare)
set -euo pipefail
HOST="$1"
out="$(mktemp)"
trap 'rm -f "$out"' EXIT
nmap -Pn -p 22,80,443,5432 "$HOST" -oG - | tee "$out"
grep -q '22/open' "$out" || { echo "SSH is not reachable"; exit 1; }
# `! cmd` does not stop a `set -e` script, so each "must not" is an explicit failure.
if grep -q '5432/open' "$out"; then echo "PostgreSQL is reachable from outside"; exit 1; fi
if curl -sk --max-time 5 "https://$HOST" -o /dev/null; then echo "HTTPS answers without Cloudflare"; exit 1; fi
echo "OPS-CHK01 passed"
