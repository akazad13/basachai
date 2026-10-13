# Property page

The property page gives a tenant everything needed to decide whether to call: photos, facts, the full cost breakdown, the owner's number on request, and a way to report problems.

Version 1.0 · 13 October 2026 · Route `/listing/{shortId}` (public URL `/bn/rent/dhaka/dhamrai/{area}/{slug}-{shortId}`) · Covers FR-06, FR-13, FR-14, FR-15

## User stories

- As a tenant, I can see every cost separately and the true monthly total before calling.
- As a tenant, I can reveal and call the owner's number without creating an account.
- As a tenant, I can save the home (signed in) or report it (anyone).
- As a tenant, I am reminded not to send money before visiting.

## Layout

| Width | Order |
| --- | --- |
| 900 px and up | Left column: gallery, header, body. Right column (360 px, sticky): cost breakdown, advertiser box |
| Under 900 px | Gallery, header, cost breakdown, advertiser box, body |

| Block | Content |
| --- | --- |
| Back link | "Back to results" (returns to the last search URL, or `/search`) |
| Gallery | Main photo (16:10, max 56% of screen height) with "Photo i of N"; thumbnail strip, scrollable, current one outlined in brass |
| Header | Freshness, title, then area, bedrooms, bathrooms, move-in |
| Cost breakdown | See [listing and pricing](../domain/listing-and-pricing.md#cost-breakdown) |
| Advertiser box | Initial avatar, owner name, badges "Owner" and "Number not verified"; Show number button; safety line; Save; Report |
| About the home | Facts grid: type, bedrooms, bathrooms, size, floor, furnishing, who can rent, available from |
| Description, Amenities, Owner's rules | Shown only when present |
| Location | "{Area}, Dhamrai, Dhaka" and the note that the owner shares the exact address by phone |

## Behaviour

### Show number

1. Tenant presses "Show number".
2. Client calls `POST /v1/listings/{shortId}/contact`.
3. On success, the button is replaced by the number in large type (Bangla digits in Bangla mode, formatted `01700-000101`) and a "Call" button linking to `tel:+8801700000101`.
4. The number is never in the page HTML before the reveal.
5. On rate limit (429): "You have viewed many numbers recently. Try again in {minutes} minutes."

### Report

1. "Report this listing" opens a dialog: reasons (Suspicious or a scam, Information is wrong, Posted more than once, No longer available) and an optional note.
2. Submitting without a reason shows "Choose a reason".
3. On success the dialog closes and a message says "Report sent. A moderator will review it within one business day."

### Save

See [saved homes](saved-homes.md). Signed-out users get the sign-in dialog with "Sign in to save homes."

### Gallery

Clicking a thumbnail swaps the main photo. Left and right arrow keys move between photos when the gallery has focus.

## States

| State | Behaviour |
| --- | --- |
| Listing not Live (expired, paused, rented, archived, rejected, hidden) | "This listing is no longer available." with "Back to home"; HTTP 200 with `noindex`; dropped from sitemap |
| Listing never existed | 404 page |
| Owner or staff viewing a non-live listing | Page renders with a status banner so they can preview |
| Photo fails to load | The drawn placeholder stays visible |

## Data and API

- `GET /v1/listings/{shortId}`: full listing without phone or address.
- `POST /v1/listings/{shortId}/contact`: returns `{ phone }`; rate-limited (see [trust and safety](../domain/trust-and-safety.md#rate-limits)).
- `POST /v1/listings/{shortId}/reports`: `{ reason, details? }`.

## SEO

Title "{title} | BasaChai", meta description from the first 155 characters of the description or a generated summary, canonical URL, Open Graph image from the cover photo.

## Analytics

`listing_viewed` on load; `contact_revealed` on reveal (with signed-in flag); `report_created` on report.

## Acceptance criteria

- **PROP-1** Given a live listing with rent 10,500, service 1,200, gas 1,080 and advance 21,000, then the breakdown shows each line, a monthly total of ৳12,780, and the advance in the one-time box only.
- **PROP-2** Given I am signed out, when I press "Show number", then the number and a Call button appear and a `contact_revealed` event is recorded.
- **PROP-3** Given the page source before reveal, then the owner's phone number is not present.
- **PROP-4** Given I exceed the reveal limit, then I see the rate-limit message and no number.
- **PROP-5** Given any listing, then "Number not verified" is shown next to the owner and no text claims verification.
- **PROP-6** Given I submit a report without a reason, then "Choose a reason" shows and nothing is sent.
- **PROP-7** Given an expired listing URL, then "This listing is no longer available" shows with `noindex`.
- **PROP-8** Given a 375 px screen, then the cost breakdown and Show number appear before the facts grid, with no horizontal scroll.
- **PROP-9** Given the full address is stored, then it never appears on the page or in the API response.

## Out of scope

Maps, virtual tours, WhatsApp, in-app chat, similar homes carousel.

## Test cases

Dataset `pilot` ([test data](../tech/quality-and-testing.md#test-data-the-pilot-dataset)). E2E runs in `bn-mobile`, `bn-desktop` and `en-desktop` unless noted.

### E2E test cases

| ID | Covers | Preconditions | Steps | Expected |
| --- | --- | --- | --- | --- |
| PROP-TC01 | PROP-1 | Seed `pilot` | 1. Open `/{lang}/listing/D3` | Breakdown lines: rent ৳10,500, service charge ৳1,200, gas bill ৳1,080; total ৳12,780; one-time box: advance ৳21,000 and key deposit ৳1,000; neither one-time amount is in the total |
| PROP-TC02 | PROP-2, PROP-3 | Seed `pilot`; signed out | 1. Open `/{lang}/listing/D3`<br>2. Read the page HTML<br>3. Press "Show number" | Step 2: owner's number absent in both scripts. Step 3: number shows formatted (`01900-001010` or Bangla digits); "Call" links to `tel:+8801900001010`; a `contact_revealed` event with `signedIn: false` exists |
| PROP-TC03 | PROP-4 | Seed `pilot`; 10 reveals already made from this browser | 1. Open `/{lang}/listing/A1`<br>2. Press "Show number" | Rate-limit message shows; no number appears |
| PROP-TC04 | PROP-5 | Seed `pilot` | 1. Open `/{lang}/listing/A1` | "Owner" and "Number not verified" badges visible next to the owner name |
| PROP-TC05 | PROP-6 | Seed `pilot` | 1. Open `/{lang}/listing/B2`<br>2. Press "Report this listing"<br>3. Press "Send report" with no reason<br>4. Choose "Information is wrong", type a note, send | Step 3: "Choose a reason" shows, dialog stays open. Step 4: dialog closes and the report-sent message shows; the moderation API lists a new open report on B2 |
| PROP-TC06 | PROP-7 | Seed `pilot` | 1. Open `/{lang}/listing/M2` (past expiry) | "This listing is no longer available" and "Back to home"; `<meta name="robots" content="noindex">` present |
| PROP-TC07 | PROP-8 | Seed `pilot`; `bn-mobile` only | 1. Open `/bn/listing/D3` | Cost breakdown and "Show number" sit above the facts grid; no horizontal scroll |
| PROP-TC08 | PROP-9 | Seed `pilot` (D3 has a stored full address) | 1. Open `/{lang}/listing/D3`<br>2. Search the page HTML for the stored address | Address text not found |

### Automation (E2E)

```ts
// web/e2e/property.spec.ts
import { test, expect } from './fixtures';
import { listing, USERS } from './seed-data';

test.beforeEach(async ({ seed }) => { await seed('pilot'); });

test('PROP-TC01 cost breakdown and totals', { tag: '@smoke' }, async ({ page, lang, t, money }) => {
  await page.goto(`/${lang}/listing/D3`);
  const box = page.getByTestId('cost-breakdown');
  await expect(box).toContainText(money(10_500));
  await expect(box).toContainText(money(1_200));
  await expect(box).toContainText(money(1_080));
  await expect(box.getByTestId('cost-total')).toHaveText(money(12_780));
  const once = box.getByRole('group', { name: t('one_time_head') });
  await expect(once).toContainText(money(21_000));
  await expect(once).toContainText(money(1_000));
});

test('PROP-TC02 number is revealed only on request', { tag: '@smoke' }, async ({ page, lang, t, api }) => {
  const owner = listing('D3').owner;                       // { phone: '01900001010', ... }
  await page.goto(`/${lang}/listing/D3`);
  const html = await page.content();
  expect(html).not.toContain(owner.phone.slice(1));
  expect(html).not.toContain('০১৯০০০০১০১০'.slice(1));
  await page.getByRole('button', { name: t('show_number') }).click();
  await expect(page.getByRole('link', { name: t('call') })).toHaveAttribute('href', 'tel:+8801900001010');
  const events = await (await api.get('/v1/test/events?name=contact_revealed')).json();
  expect(events.at(-1).props).toMatchObject({ listingId: 'D3', signedIn: false });
});

test('PROP-TC03 reveal limit', async ({ page, lang, t }) => {
  const api = process.env.E2E_API_URL;
  for (const id of ['A2', 'A3', 'B1', 'B2', 'B3', 'D1', 'D2', 'D3', 'Z2', 'Z3']) {
    expect((await page.request.post(`${api}/v1/listings/${id}/contact`)).ok()).toBeTruthy();
  }
  await page.goto(`/${lang}/listing/A1`);
  await page.getByRole('button', { name: t('show_number') }).click();
  await expect(page.getByText(t('reveal_limited').split('{')[0].trim())).toBeVisible();   // Next.js has its own role=alert announcer
  await expect(page.getByRole('link', { name: t('call') })).toHaveCount(0);
});

test('PROP-TC04 owner is labelled, number is not verified', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/listing/A1`);
  await expect(page.getByText(t('owner'), { exact: true })).toBeVisible();
  await expect(page.getByText(t('not_verified'))).toBeVisible();
});

test('PROP-TC05 report needs a reason', async ({ page, lang, t, api }) => {
  await page.goto(`/${lang}/listing/B2`);
  await page.getByRole('button', { name: t('report') }).click();
  const dialog = page.getByRole('dialog', { name: t('report_t') });
  await dialog.getByRole('button', { name: t('send_report') }).click();
  await expect(dialog.getByText(t('pick_reason'))).toBeVisible();
  await dialog.getByLabel(t('r_inaccurate')).check();
  await dialog.getByLabel(t('r_details')).fill('Rent on the phone was higher');
  await dialog.getByRole('button', { name: t('send_report') }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole('status')).toHaveText(t('report_sent'));
});

test('PROP-TC06 unavailable listing page', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/listing/M2`);
  await expect(page.getByText(t('not_found'))).toBeVisible();
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
});

test('PROP-TC07 mobile puts costs and contact first', async ({ page, t }, info) => {
  test.skip(info.project.name !== 'bn-mobile');
  await page.goto('/bn/listing/D3');
  const costY = (await page.getByTestId('cost-breakdown').boundingBox())!.y;
  const showY = (await page.getByRole('button', { name: t('show_number') }).boundingBox())!.y;
  const factsY = (await page.getByRole('region', { name: t('about_t') }).boundingBox())!.y;
  expect(costY).toBeLessThan(factsY);
  expect(showY).toBeLessThan(factsY);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});

test('PROP-TC08 full address never reaches the page', async ({ page, lang }) => {
  await page.goto(`/${lang}/listing/D3`);
  expect(await page.content()).not.toContain(listing('D3').fullAddress);
});
```

`reveal_limited` is the message key for the rate-limit text in [trust and safety](../domain/trust-and-safety.md#rate-limits).

### API test cases

| ID | Covers | Case | Expected |
| --- | --- | --- | --- |
| PROP-API01 | PROP-2 | Anonymous `POST /v1/listings/D3/contact` | 200 with phone `+8801900001010`; one contact event stored |
| PROP-API02 | PROP-9, SAFE-5 | `GET /v1/listings/D3` | Body contains neither the owner's number nor any address field |

### Automation (API)

```csharp
// tests/BasaChai.Api.Tests/PropertyTests.cs
public class PropertyTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // PROP-API01: PROP-2
    public async Task Contact_returns_phone_counts_reveal_and_records_event()
    {
        var client = await f.AsAsync("anonymous");
        var res = await client.PostAsync("/v1/listings/D3/contact", null);
        res.EnsureSuccessStatusCode();
        Assert.Equal("+8801900001010", (await res.Content.ReadFromJsonAsync<ContactDto>())!.Phone);
        await using var db = f.Db();
        Assert.Equal(1, await db.ContactEvents.CountAsync(e => e.Listing.ShortId == "D3"));
    }

    [Fact] // PROP-API02: PROP-9, SAFE-5
    public async Task Detail_never_contains_phone_or_address()
    {
        var client = await f.AsAsync("anonymous");
        var json = await client.GetStringAsync("/v1/listings/D3");
        Assert.DoesNotContain("01900001010", json);
        Assert.DoesNotContain("fullAddress", json, StringComparison.OrdinalIgnoreCase);
    }
}
```

`f.Db()` returns a scoped `AppDbContext` from the factory's service provider.

## Related

[listing and pricing](../domain/listing-and-pricing.md) · [trust and safety](../domain/trust-and-safety.md) · [saved homes](saved-homes.md) · [photos](../tech/photos.md)
