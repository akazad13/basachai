# Trust and safety

With no phone verification (D9), trust rests on moderating every new listing, honest labels, easy reporting, and limits that make scraping and abuse expensive.

Version 1.0 · 13 October 2026 · Source: Master Spec v1.2, section 11 · Covers FR-13, FR-15, FR-18

## Safeguards

| Safeguard | Rule |
| --- | --- |
| Moderation | Every new or resubmitted listing is reviewed before going Live; target within one business day |
| Live edits | Not re-reviewed (D23); every edit is audited and visible in listing history |
| Reports | Anyone can report; acknowledged within one business day; moderators can hide the listing |
| Verification label | Every contact number shows "Number not verified" (নম্বর যাচাই করা হয়নি) |
| Safety copy | Home page, property page and Safety tips page: see the home before paying anything |
| Owner-only posting | Agents cannot post at launch (D2) |
| Staff accounts | Two-factor sign-in required for moderators and admins |
| Audit | All moderation and admin actions, Live edits, password reset issue and use |

## Wording rules

- Never say or imply that BasaChai checked the advertiser, the number or the property.
- Do not use "verified", "trusted", "genuine owner" or similar on listings or in marketing.
- "Ownership verified" may only be used after a real ownership check exists.
- If phone verification is added later, it proves control of the number only, not ownership.

## Reports

| Reason | Key | Badge |
| --- | --- | --- |
| Suspicious or a scam | `suspicious` | Red |
| Information is wrong | `inaccurate` | Brass |
| Posted more than once | `duplicate` | Brass |
| No longer available | `unavailable` | Brass |

- Optional note up to 500 characters.
- Anonymous reports allowed (working default Q6), rate-limited per IP.
- Resolutions: Hide listing, or Dismiss. Both are audited.

## Privacy

- Full address encrypted with ASP.NET Core Data Protection; never public.
- Photo metadata, including GPS, removed on upload ([photos](../tech/photos.md)).
- Phone number fetched only on reveal, never in page HTML.
- IP addresses and device IDs stored only as salted hashes. The salt rotates monthly and old salts are deleted ([security and operations](../tech/security-and-operations.md)); rate limits are unaffected by the rotation.
- Users can delete their account; their listings are archived and personal data removed.

## Rate limits

In-memory on the single API instance (resets on restart; acceptable for the MVP). Client IP comes from `CF-Connecting-IP`, trusted only because the firewall accepts traffic from Cloudflare alone.

| Action | Limit | Response |
| --- | --- | --- |
| Phone reveal | 10 per hour and 30 per day per IP and device (placeholder, Q4) | 429, "You have viewed many numbers recently. Try again in {minutes} minutes." |
| Registration | 5 accounts per IP per day | 429 |
| Login | Lockout for 15 minutes after 10 failed attempts per number (attempts are counted before the password check, so parallel guesses cannot exceed it); 50 attempts per IP per 15 minutes | 429 |
| Reports | 10 per IP per day | 429 |
| Listing submission | 10 per account per day | 429 |
| Photo upload | 60 per account per hour | 429 |
| Analytics events | 300 per IP per hour; a session's views of one listing count once an hour | 429 |

## Acceptance criteria

- **SAFE-1** Given any listing page or card, then no text claims verification and "Number not verified" is shown on the property page.
- **SAFE-2** Given the 11th reveal within an hour from one IP, then 429 is returned and no number is shown.
- **SAFE-3** Given an anonymous report, then it reaches the moderation queue with no reporter ID.
- **SAFE-4** Given a hidden listing, then it is absent from search, the home page and saved homes.
- **SAFE-5** Given any public API response, then it contains no full address and no phone number (except the reveal endpoint).

## Test cases

| ID | Covers | Level | Case | Expected |
| --- | --- | --- | --- | --- |
| SAFE-TC01 | SAFE-1 | E2E | Visit home, search, every live property page, in both languages | "Number not verified" on every property page; no page contains the words in the banned list (verified, trusted, genuine owner, যাচাইকৃত, বিশ্বস্ত) outside that label |
| SAFE-API01 | SAFE-2 | API | 11 reveals from one IP within an hour | Reveals 1 to 10 succeed; the 11th returns 429 `rate_limited` with `retryAfter`; after the clock moves 61 minutes, reveals work again |
| SAFE-API02 | SAFE-3 | API | Anonymous report on B2 | 201; stored with `reporter_id` null and a `reporter_ip_hash`; listed in `/admin/reports` |
| SAFE-API03 | SAFE-4 | API | Hide A1, which the tenant has saved | A1 absent from search, from `sort=confirmed&size=4` (home) and from `/me/saved` |
| SAFE-API04 | SAFE-5 | API | Call every public GET endpoint for every live listing | No response contains any seeded owner phone (in any format) or any seeded full address |

### Automation (E2E)

```ts
// web/e2e/safety.spec.ts
import { test, expect } from './fixtures';
import { LIVE_IDS } from './seed-data';

const BANNED = [/\bverified\b/i, /\btrusted\b/i, /genuine owner/i, /যাচাইকৃত/, /বিশ্বস্ত/];

test('SAFE-TC01 no page claims verification', async ({ page, lang, t, seed }) => {
  await seed('pilot');
  for (const path of ['', '/search', ...LIVE_IDS.map(id => `/listing/${id}`)]) {
    await page.goto(`/${lang}${path}`);
    const text = (await page.locator('main').innerText()).replaceAll(t('not_verified'), '');
    for (const word of BANNED) expect(text, `${path} contains ${word}`).not.toMatch(word);
    if (path.startsWith('/listing/')) await expect(page.getByText(t('not_verified'))).toBeVisible();
  }
});
```

### Automation (API)

```csharp
// tests/BasaChai.Api.Tests/SafetyTests.cs
public class SafetyTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // SAFE-API01
    public async Task Reveal_limit_is_ten_per_hour()
    {
        var anon = await f.AsAsync("anonymous");
        string[] ids = ["A1", "A2", "A3", "B1", "B2", "B3", "D1", "D2", "D3", "Z2", "Z3"];
        var codes = new List<HttpStatusCode>();
        foreach (var id in ids) codes.Add((await anon.PostAsync($"/v1/listings/{id}/contact", null)).StatusCode);
        Assert.All(codes.Take(10), c => Assert.Equal(HttpStatusCode.OK, c));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[10]);
        f.Clock.Advance(TimeSpan.FromMinutes(61));
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsync("/v1/listings/I1/contact", null)).StatusCode);
    }

    [Fact] // SAFE-API02
    public async Task Anonymous_report_is_stored_without_a_reporter()
    {
        var anon = await f.AsAsync("anonymous");
        var res = await anon.PostAsJsonAsync("/v1/listings/B2/reports", new { Reason = "inaccurate", Details = "Rent differs" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        await using var db = f.Db();
        var report = await db.Reports.OrderByDescending(r => r.CreatedAt).FirstAsync();
        Assert.Null(report.ReporterId);
        Assert.False(string.IsNullOrEmpty(report.ReporterIpHash));
    }

    [Fact] // SAFE-API03
    public async Task Hidden_listing_leaves_search_home_and_saved()
    {
        var tenant = await f.AsAsync("tenant");
        await tenant.PutAsync("/v1/me/saved/A1", null);
        var mod = await f.AsAsync("moderator");
        (await mod.PostAsync("/v1/admin/listings/A1/hide", null)).EnsureSuccessStatusCode();
        Assert.DoesNotContain((await tenant.GetFromJsonAsync<SearchPage>("/v1/listings?size=24"))!.Items, c => c.ShortId == "A1");
        Assert.DoesNotContain((await tenant.GetFromJsonAsync<SearchPage>("/v1/listings?sort=confirmed&size=4"))!.Items, c => c.ShortId == "A1");
        Assert.DoesNotContain((await tenant.GetFromJsonAsync<List<CardDto>>("/v1/me/saved"))!, c => c.ShortId == "A1");
    }

    [Fact] // SAFE-API04
    public async Task Public_responses_never_leak_phones_or_addresses()
    {
        var anon = await f.AsAsync("anonymous");
        var bodies = new List<string> { await anon.GetStringAsync("/v1/listings?size=24") };
        foreach (var id in TestData.LiveIds) bodies.Add(await anon.GetStringAsync($"/v1/listings/{id}"));
        foreach (var body in bodies)
        foreach (var secret in TestData.OwnerPhonesAllFormats.Concat(TestData.FullAddresses))
            Assert.DoesNotContain(secret, body);
    }
}
```

## Related

[moderation and admin](../features/moderation-and-admin.md) · [property page](../features/property-page.md) · [security and operations](../tech/security-and-operations.md)
