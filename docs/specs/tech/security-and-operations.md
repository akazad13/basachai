# Security and operations

On one server, hardening the host matters as much as the application. The MVP targets about 99% monthly availability (D22) and invests in being able to rebuild quickly from off-site backups.

Version 1.1 · 13 October 2026 · Runbook: [`deploy/README.md`](../../../deploy/README.md)

## Security controls

| Area | Control |
| --- | --- |
| Firewall | `ufw`: 443 open to Cloudflare IP ranges only, and the same rule in Docker's `DOCKER-USER` chain (Docker's published ports bypass ufw); ranges refreshed weekly; SSH key-only, no root login, `fail2ban`; PostgreSQL not exposed |
| Patching | `unattended-upgrades`; monthly pulls of patched PostgreSQL and Caddy images (`deploy/patch.sh`); monthly API image rebuilds for .NET patches; Dependabot for packages and images |
| TLS | Cloudflare "Full (strict)" with a Cloudflare origin certificate on Caddy; HSTS |
| Origin | A Cloudflare Transform Rule adds a secret `X-Origin-Secret` header; Caddy refuses requests without it, so other Cloudflare zones cannot reach the origin and `CF-Connecting-IP` can be trusted |
| Secrets | `/etc/basachai/api.env`, root-owned, mode 600: database password, Cloudinary secret, Data Protection settings; never in git or CI logs |
| Auth | Identity cookie (HttpOnly, Secure, SameSite=Lax); antiforgery header; CORS to the web origin only; 2FA for staff |
| Input and output | FluentValidation on every request; EF Core parameters only; HTML stripped from owner text; escaped output in Next.js |
| Headers | Content Security Policy, `X-Content-Type-Options`, `Referrer-Policy`, frame denial |
| Address | Encrypted at column level; Data Protection key ring on its own volume and backed up |
| Privacy | Only name, phone and password hash required; IPs, device IDs and analytics sessions stored as keyed hashes with a salt that rotates every calendar month (UTC), combined with the server secret; salts older than the previous month are deleted and backups leave the salts out (`hash_salts`), so older hashes can no longer be linked to anyone; rate-limit keys and cookie signatures use the stable secret and are never stored; account deletion |
| Least privilege | Separate database roles: `basachai_migrator` owns the schema; `basachai_api` reads and writes rows only, with `audit_log` insert-only. The migrate step grants these rights |
| Dependencies | Renovate or Dependabot; vulnerability scans of NuGet, npm and container images in CI |
| Rate limits | See [trust and safety](../domain/trust-and-safety.md#rate-limits) |

## Backups and recovery

| Item | Approach |
| --- | --- |
| Database | Nightly `pg_dump` (custom format, encrypted with gpg to a public key whose private key stays offline) copied off-site with rclone (copy only: the server never deletes off-site copies; the storage's own retention expires them); keep 14 daily and 8 weekly on the server; a dead-man's-switch ping reports missed or failed runs |
| Keys | Nightly copy of the Data Protection key ring off-site (needed to decrypt addresses) |
| Photos | Live only in Cloudinary; public IDs are in the database. Accepted MVP risk: losing the Cloudinary account means owners re-upload |
| VPS snapshot | Weekly, if the provider offers it cheaply |
| Targets | RPO 24 hours; RTO about 4 hours on a fresh VPS using `deploy/` |
| Drills | Full restore before launch, then monthly |

## Monitoring

| Signal | Tool |
| --- | --- |
| Uptime | Free uptime monitor on `/health` every 5 minutes, alerting by app or email |
| Health | `/health/live` and `/health/ready` (database reachable) |
| Logs | Structured JSON to Docker with rotation; no phone numbers or addresses in logs |
| Errors | Optional free-tier error tracker |
| Capacity | Weekly look at the provider's CPU, RAM and disk graphs; act on [roadmap](../roadmap.md) triggers |
| Cloudinary | Usage shown in the admin dashboard |
| Reports SLA | Admin dashboard highlights reports older than one business day |

## Deploy

1. CI (free tier of GitHub Actions or Azure Pipelines) runs tests and builds the API image to a container registry.
2. The deploy script (`deploy/deploy.sh`) connects over SSH, pulls the image, runs the image's `migrate` command with the migration role (EF Core migrations, reference data and the API role's grants), then starts the new API and waits for its health check.
3. Migrations stay backward compatible (expand, then contract), so the previous image can be restarted (`deploy/rollback.sh`) if the new one fails its health check.
4. Next.js deploys through Netlify's Git pipeline, with preview deploys for pull requests.
5. Staging runs as a separate Compose project on the VPS before releases, with its own database and key ring. Production's Caddy serves the staging host over a shared Docker network, so staging opens no port.

## Acceptance criteria

- **OPS-1** Given the VPS, then only port 443 from Cloudflare ranges and SSH by key are reachable.
- **OPS-2** Given a nightly backup, when restored to a fresh server with `deploy/`, then the site works and stored addresses decrypt.
- **OPS-3** Given the API is stopped, then the uptime monitor alerts within 10 minutes.
- **OPS-4** Given a failing health check after deploy, then the previous image can be restarted without a database restore.
- **OPS-5** Given any log line, then it contains no phone number or address.

## Test cases

Operational checks run against staging before the first production deploy and after any server change; OPS-CHK02 and CHK03 repeat monthly. Scripts live in `deploy/checks/`.

| ID | Covers | Check | Pass when |
| --- | --- | --- | --- |
| OPS-CHK01 | OPS-1 | Port scan the VPS's public IP from outside Cloudflare | Only 22 (key auth) answers; 443 refuses non-Cloudflare sources; 5432 closed |
| OPS-CHK02 | OPS-2 | Restore the latest off-site backup onto a fresh VPS with `deploy/restore.sh`, then run the smoke suite | Smoke suite passes; a stored address decrypts on an owner's edit screen |
| OPS-CHK03 | OPS-3 | `docker compose stop api` on staging | Uptime monitor alerts within 10 minutes; recovers after start |
| OPS-CHK04 | OPS-4 | `deploy/checks/rollback-drill.sh staging`: releases an image whose health check fails, then runs `deploy/rollback.sh` | Previous image serves traffic without a database restore |
| OPS-CHK05 | OPS-5 | Run the full E2E suite on staging, then scan the API logs | No log line matches a Bangladeshi phone pattern or any seeded address |

### Automation

```bash
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
```

```bash
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
```

The smoke suite for OPS-CHK02 is the Playwright tag `@smoke` (`npx playwright test --grep @smoke`): HOME-TC01, SRCH-TC01, PROP-TC01, PROP-TC02 and DASH-TC01. Runs against staging set `E2E_API_URL`, `E2E_WEB_URL` and `E2E_TEST_KEY` (the staging hook key, sent as `X-Test-Key`).

API logs are JSON in Staging and Production. Request logging and SQL command logging are at Warning, and SQL parameter values are never logged.

## Related

[architecture](architecture.md) · [trust and safety](../domain/trust-and-safety.md) · [quality and testing](quality-and-testing.md)
