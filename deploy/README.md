# Deploy and operations

How the API runs on one VPS, and how to set it up, release, back up, restore and check it. The rules behind this are in [security and operations](../../specs/tech/security-and-operations.md). The web app deploys separately, through Netlify (`netlify.toml`).

| File | What it does |
| --- | --- |
| `docker-compose.yml` | Caddy, the API and PostgreSQL 17, with the memory limits and log rotation from the architecture spec |
| `Caddyfile` | Origin TLS (Cloudflare origin certificate), HSTS, 12 MB body limit, the origin-secret check; serves the production and staging API hosts |
| `api.Dockerfile` | The API image (build from the repository root) |
| `compose.sh <env> ...` | `docker compose` for `production`, `staging` or `local` |
| `env/*.env` | Settings per environment that are not secret |
| `config-examples/` | The secret and per-site files to copy into `/etc/basachai` (or `/etc/basachai-staging`) |
| `setup-server.sh` | One-time hardening of a fresh Ubuntu 24.04 VPS |
| `firewall.sh` | ufw and Docker rules: SSH, and HTTPS from Cloudflare only (weekly cron) |
| `deploy.sh`, `server-release.sh`, `release.sh` | Release an image: upload `deploy/`, migrate, switch, wait for the health check |
| `rollback.sh` | Restart the last good image without touching the database |
| `backup.sh`, `restore.sh` | Nightly encrypted backups (database and key ring) and the restore |
| `patch.sh` | Monthly: patched PostgreSQL and Caddy images |
| `checks/` | Scripted operational checks (OPS-CHK01, 04, 05) |

## How it fits together

- Production and staging are separate Compose projects (`basachai`, `basachai-staging`) with their own database, key ring and settings. Production's Caddy serves both API host names and reaches each API over the shared `basachai-edge` Docker network, so staging needs no open port. Stop staging between releases; its host then answers 502.
- PostgreSQL has two roles. `basachai_migrator` owns the database and runs migrations. `basachai_api` is what the API connects as: it reads and writes rows, cannot change the schema, and can only insert into `audit_log`. The API image's `migrate` command applies migrations and reference data with the migrator role, then grants the API role those rights.
- Migrations are expand-then-contract, so the previous image keeps working against a migrated database. That is what makes `rollback.sh` safe without a restore.
- Docker publishes Caddy's port through its own iptables chains, which bypass ufw. `firewall.sh` therefore applies the Cloudflare-only rule in Docker's `DOCKER-USER` chain as well as in ufw.
- The firewall admits every Cloudflare address, and any Cloudflare customer could point a zone at this server. So a Cloudflare Transform Rule on our zone adds a secret `X-Origin-Secret` header, and Caddy refuses requests without it. That also makes `CF-Connecting-IP`, which the API uses for rate limits, trustworthy.
- CI may run exactly one command as root on the server: `/usr/local/sbin/basachai-release`. It installs the uploaded `deploy/` root-owned and runs `release.sh`. Deploying code is still root-equivalent, so protect the `production` environment in GitHub with required reviewers.
- `release.sh` records the last image that passed its health check; `rollback.sh` always returns to it, never to a failed release.
- Release, rollback, backup, restore and patch take a lock per environment, so they never overlap.

## First-time setup

1. Create the VPS with Ubuntu 24.04 and an admin user that has your SSH key.
2. Optionally create a separate `deploy` user with its own SSH key for CI.
3. Copy `deploy/` to `/opt/basachai/deploy` (owned by root) and run `sudo bash /opt/basachai/deploy/setup-server.sh <admin-user> [<deploy-user>]`. This installs Docker, fail2ban and unattended upgrades, turns off password and root SSH logins, adds 4 GB of swap, applies the firewall, installs `basachai-release` with its sudo rule, and adds the backup, firewall and patch cron jobs.
4. Copy `config-examples/*.env.example` into `/etc/basachai/` without the `.example` suffix and fill them in (mode 600, root). Use long random passwords of letters and digits (a `$` would be read as a variable). The database passwords in `db.env` must match the ones in `api.env` and `migrate.env`. Keep a copy of this folder in your password manager: backups do not include it.
5. Put the Cloudflare origin certificate at `/etc/basachai/certs/origin.pem` and `origin.key`. In Cloudflare, set SSL to "Full (strict)", and add a Transform Rule ("Modify request header") that sets `X-Origin-Secret` to the `ORIGIN_SECRET` from `caddy.env` for the API host names.
6. Let Docker pull the private API image: `sudo docker login ghcr.io` with a GitHub token that can only read packages.
7. Create a backup key pair on your own machine (`gpg --quick-gen-key "BasaChai backups"`), copy only the public key to `/etc/basachai/backup-public.asc`, and keep the private key offline. Configure the off-site copy with `sudo rclone config` (the remote in `backup.env`). Give it credentials that can write but not delete, and set the bucket's own retention (for example delete after 120 days): `backup.sh` copies and never deletes off-site. Set `BACKUP_PING_URL` to a dead-man's-switch check (for example healthchecks.io), so a missed or failed backup alerts you.
8. Do the same for staging in `/etc/basachai-staging`, with database `basachai_staging` and a `Test__HookKey`.
9. In GitHub, add the secrets `DEPLOY_HOST` (`user@host`), `DEPLOY_SSH_KEY` and `DEPLOY_KNOWN_HOSTS`, and required reviewers on the `production` environment.
10. Release: run the CI workflow manually on `main` with the environment, or `deploy/deploy.sh <user@host> production <image>`.
11. Configure Cloudinary (upload preset and the named sizes): `sudo deploy/compose.sh production run --rm --no-deps api cloudinary-setup`. Then, in the Cloudinary console, turn on Strict transformations and two-factor sign-in. Do the same for the test account the nightly CI job uses (`CLOUDINARY_TEST_*` secrets).
12. Add an uptime monitor (any free service) on `https://api.<domain>/health` every 5 minutes, alerting by app or email (OPS-3).

## Everyday tasks

| Task | Command (on the server, as root) |
| --- | --- |
| Release | From CI, or `deploy/deploy.sh <host> <env> <image>` from your machine |
| Roll back | `deploy/rollback.sh production` |
| Logs | `deploy/compose.sh production logs -f api` |
| Back up now | `deploy/backup.sh production` |
| Restore | Import the private key (`gpg --import`), then `deploy/restore.sh production latest --yes`, then delete the private key (`gpg --delete-secret-keys`). On a fresh server, first run `setup-server.sh`, restore `/etc/basachai` from your copy, and set `API_IMAGE` in `release.env` |
| Start or stop staging | `deploy/compose.sh staging up -d` / `deploy/compose.sh staging stop` |

## Operational checks

Run against staging before the first production release and after any server change. Repeat CHK02 and CHK03 monthly.

| Check | How |
| --- | --- |
| OPS-CHK01 | `deploy/checks/ports.sh <vps-ip>` from a machine outside Cloudflare |
| OPS-CHK02 | Restore the latest off-site backup onto a fresh VPS (`setup-server.sh`, then `restore.sh ... latest --yes`). Open an owner's edit screen and check a stored address shows, then run the smoke suite: `npx playwright test --grep @smoke` with `E2E_API_URL`, `E2E_WEB_URL` and `E2E_TEST_KEY` set |
| OPS-CHK03 | `deploy/compose.sh staging stop api` and confirm the uptime monitor alerts within 10 minutes |
| OPS-CHK04 | `deploy/checks/rollback-drill.sh staging` |
| OPS-CHK05 | Run the full E2E suite against staging (with `E2E_TEST_KEY`), then `deploy/checks/logs-no-pii.sh staging` |

## Running the stack locally

`deploy/env/local.env` runs the production setup on your machine with Caddy's local certificate on https://localhost:8443. Create `deploy/local-config/` (git-ignored) from `config-examples/`, with `API_DOMAIN=localhost` in `site.env` and `API_IMAGE=basachai-api:local` in `release.env`. Then:

```bash
docker build -f deploy/api.Dockerfile -t basachai-api:local .
PULL=0 deploy/release.sh local basachai-api:local
```

The scripts are for Linux. On Windows, run them in a Linux container that uses the host's Docker, or in Git Bash with `MSYS_NO_PATHCONV=1`.
