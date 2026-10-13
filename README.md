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
dotnet test                      # 103 domain + 93 API tests; needs Docker for PostgreSQL
cd web; npx playwright test      # E2E: needs the API (seedable) and the web app running
```

The Playwright files are copied from the specs: change a test in its spec, then run `node scripts/extract-e2e.mjs` in `web/`.

## Configuration

| Setting | Where | Notes |
| --- | --- | --- |
| `ConnectionStrings:Main` | API | PostgreSQL |
| `Hashing:Salt` | API | Required in Production (16+ characters); salts IP and device hashes |
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
