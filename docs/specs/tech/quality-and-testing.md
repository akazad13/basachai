# Quality and testing

The site must be fast on a mid-range Android phone over 4G, accessible, private by default and recoverable. Every acceptance criterion becomes an automated test that runs against one shared, predictable dataset. Each spec carries its own **Test cases** section; full journeys are in [journey.md](../journey.md).

Version 1.1 · 13 October 2026 · Source: Master Spec v1.2, sections 16 and 17

## Non-functional targets

| Area | Target |
| --- | --- |
| Page speed | Largest Contentful Paint under 2.5 s on a mid-range Android phone over 4G |
| API speed | Search p95 under 500 ms server time on the VPS |
| Availability | About 99% monthly (D22) |
| Accessibility | WCAG 2.2 AA |
| Responsive | 360 px phones to desktop; no horizontal scroll |
| Localization | Correct Bangla rendering and conjuncts; Bangla digits; BDT and phone formats |
| Privacy | No phone or address in logs, events or public responses |
| Recovery | RPO 24 hours, RTO about 4 hours, restore rehearsed |

## Test levels

| Level | Tools | Lives in | Covers |
| --- | --- | --- | --- |
| Unit | xUnit; NSubstitute or hand-written fakes (Moq banned, D21) | `tests/BasaChai.Domain.Tests`, `tests/BasaChai.Application.Tests` | State machine, `EffectiveStatus`, cost totals, phone normalisation, validators, mappers |
| API integration | xUnit, WebApplicationFactory, Testcontainers PostgreSQL 17, `FakeTimeProvider` | `tests/BasaChai.Api.Tests` | Endpoints, search SQL, transitions, rate limits, auth, audit writes |
| End to end | Playwright (TypeScript) | `web/e2e` | Every feature test case and every journey |
| Accessibility | `@axe-core/playwright` | `web/e2e/a11y.spec.ts` | Every screen, both languages |
| Load | k6 | `perf/` | Search and property pages before launch |
| Security | OWASP ZAP baseline | CI against staging | Headers, injection, auth bypass |

## Test data: the `pilot` dataset

One seed file, `deploy/seed/pilot.json`, is loaded by the API's test seeder and imported by the Playwright tests (`web/e2e/seed-data.ts`), so expected values never drift. It mirrors the [prototype](../../BasaChai-Prototype.html). Ages are relative to the seed time.

### Users

| Key | Name | Role | Notes |
| --- | --- | --- | --- |
| `tenant` | Test Tenant | Member | No saved homes |
| `owner` | নাসরিন আক্তার (Nasrin Akter) | Owner | Owns Z1, M1 to M5 |
| `owner2` | আব্দুল কাদের (Abdul Kader) | Owner | Owns A1, D1 |
| `moderator` | Test Moderator | Moderator | 2FA pre-enrolled in the test environment |
| `admin` | Test Admin | Admin | 2FA pre-enrolled |
| `moderator_new` | New Moderator | Moderator | No 2FA yet (AUTH-TC04) |

Phone numbers are in the seed file; the shared test password comes from the `E2E_PASSWORD` environment variable.

### Listings

| ID | Area | Status | Rent | Service | Other monthly | Advance | Beds | Who | Confirmed |
| --- | --- | --- | --: | --: | --- | --: | --: | --- | --- |
| A1 | amin | Live | 15,000 | 1,500 | gas 1,080 | 30,000 | 3 | family | 2 days ago |
| A2 | amin | Live | 18,000 | 2,000 | | 36,000 | 2 | any | 5 days ago |
| A3 | amin | Live | 8,500 | 800 | | 17,000 | 0 | students | 9 days ago |
| B1 | barat | Live | 6,000 | 0 | electricity (by meter) | 12,000 | 2 | family | 1 day ago |
| B2 | barat | Live | 9,000 | 600 | gas 1,080 | 18,000 | 2 | any | 4 days ago |
| B3 | barat | Live | 4,500 | 0 | electricity (by meter) | not specified | 1 | bachelor | 11 days ago |
| D1 | dokkhin | Live | 13,000 | 0 | | 26,000 | 3 | family | 7 days ago |
| D2 | dokkhin | Live | 7,500 | 500 | | 15,000 | 2 | students | 12 days ago |
| D3 | dokkhin | Live | 10,500 | 1,200 | gas 1,080; key deposit 1,000 one-time | 21,000 | 2 | any | 3 days ago |
| Z1 | bazar | Live (owner) | 12,000 | 1,000 | gas 1,080 | 24,000 | 2 | family | 28 days ago |
| Z2 | bazar | Live | 9,000 | 800 | | 18,000 | 0 | students | 10 days ago |
| Z3 | bazar | Live | 6,500 | 300 | | 13,000 | 1 | any | 6 days ago |
| I1 | islampur | Live | 14,000 | 1,000 | | 28,000 | 3 | family | 2 days ago |
| I2 | islampur | Live | 8,000 | 0 | electricity (by meter) | 16,000 | 3 | family | 8 days ago |
| I3 | islampur | Live | 8,500 | 500 | | not specified | 2 | any | 14 days ago |
| M1 | barat | Draft (owner), left at step 2 | 7,500 | | | | 2 | any | started 2 days ago; no photos |
| M2 | islampur | Live, past expiry (owner) | 6,500 | 500 | | 13,000 | 1 | any | 33 days ago |
| M3 | amin | Changes needed (owner) | 16,000 | 2,000 | | 32,000 | 3 | family | submitted 30 h ago |
| M4 | barat | In review (owner) | 8,500 | 0 | | 17,000 | 3 | family | submitted 5 h ago |
| M5 | dokkhin | Rented (owner) | 9,500 | 800 | | 19,000 | 2 | family | 15 days ago |
| P1 | bazar | In review | 9,000 | 700 | | 18,000 | 2 | any | submitted 30 h ago |
| P2 | amin | In review | 7,000 | 500 | | not specified | 1 | bachelor | submitted 3 h ago |

Reports: **R1** on Z2, suspicious, 30 hours old; **R2** on I2, no longer available, 4 hours old.

In this dataset each listing's `short_id` equals its ID above (`A1`, `D3`, ...), so tests can address listings directly. D3 and Z1 have stored full addresses (in `pilot.json` and listed in `deploy/seed/addresses.txt`) for the privacy tests. Seeded photos point at free-licence demo images (`seed/{id}` public ids); development and test runs without Cloudinary keep uploads on the API's disk (`LocalPhotoStore`, never in Staging or Production). The home page cost example setting points to D3.

### Values tests rely on

| Fact | Value |
| --- | --- |
| Live, searchable listings | 15 (A1 to I3; M2 is past expiry) |
| Dokkhin Para results | 3 (D1, D2, D3) |
| Rent 8,000 to 12,000 | 7 (A3, B2, D3, Z1, Z2, I2, I3) |
| Who can rent: Family | 11 (family or any) |
| Barat Nagor with 3 bedrooms | 0 |
| 3 bedrooms, all areas | 4 (A1, D1, I1, I2) |
| Home page cost example | D3 (admin setting in the seed), total ৳12,780 |
| D3 monthly total | ৳12,780 |
| Most recently confirmed | B1 |
| Owner attention items | 3 (Z1 expiring in 2 days, M2 expired, M3 changes needed) |
| Moderation queue, oldest first | P1 (overdue), M4, P2 |
| Overdue count | 2 (P1, R1) |

## Test hooks

Enabled only when `ASPNETCORE_ENVIRONMENT=Test` or `Staging`; the endpoints do not exist in production builds.

| Method | Path | Effect |
| --- | --- | --- |
| POST | `/v1/test/seed` | `{ dataset: "pilot" or "empty" }`: wipe and reseed; resets rate limits and the clock |
| POST | `/v1/test/clock` | `{ offsetDays }`: moves the API's `TimeProvider` forward |
| POST | `/v1/test/act` | `{ listingId, action, data? }`: runs an action (`pause`, `hide`, `approve`, `mark-rented`, `edit` with field changes, ...) as the right actor, for test setup only |
| GET | `/v1/test/listings/{id}` | A listing's stored row, for assertions |
| GET | `/v1/test/audit?entity=listing&id=Z1` | Latest audit entries, for assertions |
| GET | `/v1/test/events?name=contact_revealed` | Latest analytics events, for assertions |

The web app shows times from API values, so clock offsets change statuses, days left and banners.

## Playwright setup

### Projects

```ts
// web/playwright.config.ts
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,            // tests share one seeded database
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  use: { baseURL: process.env.E2E_WEB_URL ?? 'http://localhost:3000', trace: 'retain-on-failure' },
  projects: [
    { name: 'bn-mobile', use: { ...devices['Pixel 7'], lang: 'bn' }, testIgnore: '**/journeys/**' },
    { name: 'bn-desktop', use: { viewport: { width: 1280, height: 900 }, lang: 'bn' } },
    { name: 'en-desktop', use: { viewport: { width: 1280, height: 900 }, lang: 'en' }, testIgnore: '**/journeys/**' },
  ],
});
```

Every feature test runs in all three projects unless it says otherwise with `test.skip`. Journeys (`e2e/journeys`) run once, in `bn-desktop`: they open their own phone and desktop actors and use Bangla paths.

### Fixtures

```ts
// web/e2e/fixtures.ts
import { test as base, expect, type APIRequestContext } from '@playwright/test';
import bn from '../messages/bn.json';
import en from '../messages/en.json';
import { USERS } from './seed-data';

export type Lang = 'bn' | 'en';
type Role = keyof typeof USERS;
const MESSAGES: Record<Lang, Record<string, string>> = { bn, en };
const API = process.env.E2E_API_URL ?? 'http://localhost:5080';

type Fixtures = {
  lang: Lang;
  t: (key: string, vars?: Record<string, string | number>) => string;
  num: (n: number) => string;
  money: (n: number) => string;
  api: APIRequestContext;
  seed: (dataset?: 'pilot' | 'empty') => Promise<void>;
  clock: (offsetDays: number) => Promise<void>;
  act: (listingId: string, action: string, data?: Record<string, unknown>) => Promise<void>;
  signInAs: (role: Role) => Promise<void>;
};

export const test = base.extend<Fixtures>({
  lang: ['bn', { option: true }],
  t: async ({ lang }, use) => {
    await use((key, vars = {}) =>
      Object.entries(vars).reduce((s, [k, v]) => s.replaceAll(`{${k}}`, String(v)), MESSAGES[lang][key] ?? key));
  },
  num: async ({ lang }, use) => {
    const f = new Intl.NumberFormat(lang === 'bn' ? 'bn-BD' : 'en-IN');
    await use(n => f.format(n));
  },
  money: async ({ num }, use) => { await use(n => `৳${num(n)}`); },
  api: async ({ playwright }, use) => {
    // Staging's test hooks need the hook key (Test:HookKey); local and CI runs leave E2E_TEST_KEY unset.
    const key = process.env.E2E_TEST_KEY;
    const ctx = await playwright.request.newContext({ baseURL: API, extraHTTPHeaders: key ? { 'X-Test-Key': key } : {} });
    await use(ctx);
    await ctx.dispose();
  },
  seed: async ({ api }, use) => {
    await use(async (dataset = 'pilot') => {
      expect((await api.post('/v1/test/seed', { data: { dataset } })).ok()).toBeTruthy();
    });
  },
  clock: async ({ api }, use) => {
    await use(async offsetDays => {
      expect((await api.post('/v1/test/clock', { data: { offsetDays } })).ok()).toBeTruthy();
    });
  },
  act: async ({ api }, use) => {
    await use(async (listingId, action, data) => {
      expect((await api.post('/v1/test/act', { data: { listingId, action, data } })).ok()).toBeTruthy();
    });
  },
  signInAs: async ({ page }, use) => {
    // page.request shares cookies with the page, so the browser is signed in afterwards.
    await use(async role => {
      const res = await page.request.post(`${API}/v1/auth/login`, {
        data: { phone: USERS[role].phone, password: process.env.E2E_PASSWORD },
      });
      expect(res.ok()).toBeTruthy();
    });
  },
});

export { expect };
```

The test files in `web/e2e` are copied from these specs by `web/scripts/extract-e2e.mjs`: change a test in its spec, then re-extract.

`web/e2e/seed-data.ts` imports `deploy/seed/pilot.json` and exports `USERS`, `AREAS`, `LIVE_IDS` and `listing(id)` (titles in both languages, area, amounts, owner, reason, full address).

`web/e2e/helpers.ts` holds shared steps: `signInInDialog`, `fillStep1`, `fillStep2`, `photos(names)` (paths in `web/e2e/files/`) and `publicIdOf(src)`. Photo fixtures: `room-1.jpg` to `room-4.jpg` and `room-gps.jpg` (with GPS EXIF), all under 1 MB.

### Selector rules

- Prefer roles and accessible names from the message files: `page.getByRole('button', { name: t('show_number') })`.
- Sections are `<section aria-labelledby>` so `getByRole('region', { name })` works.
- `data-testid` only for layout containers with no accessible name: `recent-grid`, `results-list`, `cost-breakdown`, `cost-total`, `attention-banners`, `review-queue`, `reports-list`.
- Listing cards are `<article>` elements; rows on the dashboard are `<article>` too.

## API integration setup

```csharp
// tests/BasaChai.Api.Tests/ApiFactory.cs
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().WithImage("postgres:17").Build();

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-10-13T06:00:00Z"));
    public FakePhotoStore Photos { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("ConnectionStrings:Main", _db.GetConnectionString());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IPhotoStore>();
            services.AddSingleton<IPhotoStore>(Photos);   // hand-written fake, no Moq
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        await ResetAsync();
    }

    public async Task ResetAsync(string dataset = "pilot")
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TestSeeder>().SeedAsync(dataset, Clock.GetUtcNow());
    }

    Task IAsyncLifetime.DisposeAsync() => _db.DisposeAsync().AsTask();
}

// tests/BasaChai.Api.Tests/TestClient.cs
public static class TestClient
{
    // Returns an HttpClient with cookies, signed in as the seeded user, sending the antiforgery header.
    public static async Task<HttpClient> AsAsync(this ApiFactory f, string role)
    {
        var client = f.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        if (role == "anonymous") return client;
        var user = TestUsers.Get(role);
        var res = await client.PostAsJsonAsync("/v1/auth/login", new { user.Phone, Password = TestUsers.Password });
        res.EnsureSuccessStatusCode();
        await client.AddAntiforgeryHeaderAsync();
        return client;
    }
}
```

- Each test class uses `IClassFixture<ApiFactory>` and calls `ResetAsync()` in its constructor or `InitializeAsync`.
- `FakeTimeProvider` comes from `Microsoft.Extensions.TimeProvider.Testing`.
- Assertions on audit and events read the database through `factory.Services`.

## Naming and traceability

- Test names start with the test case ID: `test('SRCH-TC02 rent filter uses monthly rent', ...)`, `[Fact] public async Task SRCH_API01_...`.
- Each test case lists the acceptance criteria it covers. CI publishes a report grouping results by test case ID.

## Coverage index

| Spec | Acceptance criteria | Test cases |
| --- | --- | --- |
| [Home](../features/home.md) | HOME-1 to HOME-5 | HOME-TC01 to TC05 |
| [Search](../features/search.md) | SRCH-1 to SRCH-8 | SRCH-TC01 to TC08, SRCH-API01 to API03 |
| [Property page](../features/property-page.md) | PROP-1 to PROP-9 | PROP-TC01 to TC08, PROP-API01 to API02 |
| [Auth and account](../features/auth-and-account.md) | AUTH-1 to AUTH-7 | AUTH-TC01 to TC05, AUTH-API01 to API03 |
| [Saved homes](../features/saved-homes.md) | SAVE-1 to SAVE-4 | SAVE-TC01 to TC04 |
| [Post a property](../features/post-property.md) | POST-1 to POST-9 | POST-TC01 to TC07, POST-API01 to API02 |
| [Owner dashboard](../features/owner-dashboard.md) | DASH-1 to DASH-6 | DASH-TC01 to TC07 |
| [Moderation and admin](../features/moderation-and-admin.md) | MOD-1 to MOD-7 | MOD-TC01 to TC05, MOD-API01 to API03 |
| [Listing and pricing](../domain/listing-and-pricing.md) | PRICE-1 to PRICE-5 | PRICE-UT01 to UT05 |
| [Listing lifecycle](../domain/listing-lifecycle.md) | LIFE-1 to LIFE-7 | LIFE-UT01 to UT04, LIFE-API01 to API04 |
| [Trust and safety](../domain/trust-and-safety.md) | SAFE-1 to SAFE-5 | SAFE-TC01, SAFE-API01 to API04 |
| [Design system](../design/design-system.md) | DS-1 to DS-5 | DS-TC01 to TC04 |
| [Content and localization](../design/content-and-localization.md) | L10N-1 to L10N-5 | L10N-TC01 to TC03, L10N-CI01 |
| [Photos](photos.md) | PHOTO-1 to PHOTO-5 | PHOTO-API01 to API05 |
| [Security and operations](security-and-operations.md) | OPS-1 to OPS-5 | OPS-CHK01 to CHK05 |
| [Analytics](analytics.md) | ANA-1 to ANA-3 | ANA-API01 to API03 |
| [Journeys](../journey.md) | Launch checklist | JRN-01 to JRN-07 |

## Definition of done (per ticket)

- Test cases in the spec automated and passing in CI in all three Playwright projects.
- Bangla and English copy in message files; no hard-coded strings.
- Checked at 375 px and 1,280 px; keyboard and screen-reader labels checked; axe reports no violations.
- No new dependency on Moq or AutoMapper.
- Audit and analytics events written where the spec says so, with an assertion in a test.

## Related

[journey](../journey.md) · [requirements](../requirements.md) · [security and operations](security-and-operations.md)
