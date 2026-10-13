# BasaChai

Bangla-first rental marketplace, MVP for the Dhamrai pilot. The specs in [`docs/specs`](docs/specs/README.md) are the source of truth; the [master spec](docs/BasaChai-Master-Spec.html) wins if anything disagrees.

| Part | Stack | Folder |
| --- | --- | --- |
| API | ASP.NET Core on .NET 10, EF Core + PostgreSQL 17 | `src/` |
| Web | Next.js 15 (App Router, SSR), next-intl | `web/` |
| Tests | xUnit + Testcontainers (API), Playwright (web) | `tests/`, `web/e2e/` |
| Seed data | The `pilot` dataset shared by every test | `deploy/seed/pilot.json` |
| Docs | Specs (source of truth), master spec, PRD, technical designs, prototype | `docs/` |

## Run it locally

Needs Docker, the .NET 10 SDK and Node 22.

1. Copy `.dev.env.example` to `.dev.env` and fill in a database password and a test password (`E2E_PASSWORD` and `Seed__Password`, the same value). `.dev.env` is git-ignored.
2. Start PostgreSQL and the API (Test environment, so the seed and clock hooks exist) on http://localhost:5080:

   ```powershell
   ./scripts/dev-api.ps1
   ```

3. Load the pilot data: `POST http://localhost:5080/v1/test/seed` with `{"dataset":"pilot"}`.
4. Start the web app on http://localhost:3000:

   ```powershell
   cd web; npm install; npm run dev
   ```

Without Cloudinary keys, uploads in Development and Test are kept on the API's disk and served from `/v1/dev-photos`. Seeded listings use free-licence demo photos.

## Tests

```powershell
dotnet test                      # 103 domain + 117 API tests (2 skip without Cloudinary keys); needs Docker
cd web; npx playwright test      # 193 E2E runs across bn-mobile, bn-desktop, en-desktop; needs the API and web app
```

The Playwright files are copied from the specs: change a test in its spec, then run `node scripts/extract-e2e.mjs` in `web/`.

## Configuration

| Setting | Where | Notes |
| --- | --- | --- |
| `ConnectionStrings:Main` | API | PostgreSQL |
| `Hashing:Salt` | API | Required in Production (16+ characters); server secret combined with a monthly salt (kept in the database) for IP and device hashes |
| `Auth:CookieDomain` | API | `.{domain}` so the web app and API share the session |
| `Web:Origin` | API | CORS origin and reset-link base |
| `Web:RevalidateUrl`, `Web:RevalidateSecret` | API | The web app's `/api/revalidate`, to purge cached pages after changes |
| `Cloudinary:CloudName`, `ApiKey`, `ApiSecret`, `UploadPreset` | API | Photo storage (signed preset with the 1,600 px incoming transformation) |
| `DataProtection:KeyPath` | API | Persist the key ring (it decrypts stored addresses) and back it up |
| `Network:KnownProxies` | API | Caddy's address, so forwarded headers are trusted |
| `Test:HookKey` | API | Required in Staging; test hooks then need the `X-Test-Key` header |
| `NEXT_PUBLIC_API_URL`, `API_URL` | Web | Browser and server-side API base URLs |
| `NEXT_PUBLIC_CLOUDINARY_CLOUD` | Web | Builds photo URLs |
| `REVALIDATE_SECRET` | Web | Same value as the API's `Web:RevalidateSecret` |

## Status

Everything in the MVP specs is built and tested. What remains needs accounts, a server or decisions:

| Item | Needs | Spec |
| --- | --- | --- |
| Server setup, firewall, SSH deploy, port check (OPS-CHK01) | A VPS (Q1) | [security and operations](docs/specs/tech/security-and-operations.md) |
| First CI run | The repository on GitHub | `.github/workflows/ci.yml` |
| Page caching on Netlify's edge | A staging deploy; check an owner's edit shows at once | [content and localization](docs/specs/design/content-and-localization.md#seo) |
| The two skipped photo tests | A Cloudinary test account and keys | [photos](docs/specs/tech/photos.md) |
| Restore drill on a fresh server (OPS-CHK02) | Off-site backup storage (Q3) | [security and operations](docs/specs/tech/security-and-operations.md) |
| Uptime alert check (OPS-CHK03) | An uptime monitor | [security and operations](docs/specs/tech/security-and-operations.md) |
| Area spelling aliases, admin reset identity check, brand and legal review | Decisions Q5, Q7, Q10 | [open questions](docs/specs/open-questions.md) |

Known and accepted: the content security policy allows inline scripts, because per-request nonces would switch off page caching.
