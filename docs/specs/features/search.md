# Search and results

Tenants filter live rentals by area, rent and home details; filters combine with AND, live in the URL, and never match unknown values.

Version 1.0 · 13 October 2026 · Route `/search` · Covers FR-01 to FR-05, FR-16

## User stories

- As a tenant, I can narrow homes by area, budget, bedrooms, bathrooms, type, furnishing, who can rent and move-in date.
- As a tenant, I can sort results and share a search link that opens the same results.
- As a tenant on a phone, I can open the filters in a sheet without losing my place.
- As a tenant, when nothing matches, I get a one-tap way to widen the search.

## Filters

| Filter | URL key | Control | Rule |
| --- | --- | --- | --- |
| Area | `area` | Select: All of Dhamrai + 5 areas | Matches the listing's area (and its sub-areas when added) |
| Min rent | `min` | Number, step 500 | `rent >= min` |
| Max rent | `max` | Number, step 500 | `rent <= max` |
| Bedrooms | `beds` | Select: Any, Studio, 1 to 4 | Exact match; Studio = 0 |
| Bathrooms | `baths` | Select: Any, 1 to 3 | Exact match |
| Type | `type` | Select: Any, Flat, Semi-pucca house | Exact match |
| Furnishing | `furnish` | Select: Any, Furnished, Semi-furnished, Unfurnished | Exact match |
| Who can rent | `occ` | Select: Any, Family, Bachelor, Students | Matches listings for that group or for "anyone" |
| Move-in | `avail` | Select: Any, Now, Within a month | Now: available today; month: available within 30 days |
| Keywords | `q` | Text (results header, optional in MVP UI) | Full text over title, description and amenities |
| Sort | `sort` | Select | `confirmed` (default), `newest`, `rent_asc`, `rent_desc` |

- Rent filters use **monthly rent**, not the monthly total.
- A listing with an unknown value (for example no bathroom count) never matches a filter on that field.
- Only Live, unhidden listings within 30 days of confirmation are searchable (see [lifecycle](../domain/listing-lifecycle.md)).
- No paid ranking.

## Layout

| Width | Filters | Results |
| --- | --- | --- |
| 861 px and up | Sticky side panel, 280 px | Horizontal cards: photo left (260 px), details right |
| 640 to 860 px | Slide-up sheet opened by "Filters (n)" | Horizontal cards |
| Under 640 px | Slide-up sheet | Stacked cards |

Results header: title "Homes for rent: {area or All of Dhamrai}", live count ("১৫টি বাসা পাওয়া গেছে"), Filters button (phones only), Sort select, then removable filter chips.

### Card

| Element | Rule |
| --- | --- |
| Photo | Cover photo, 4:3, "Photo 1 of N" tag; links to the property page |
| Freshness | Dot and "Confirmed {relative time}"; dot green if confirmed within 7 days, brass after |
| Save | Toggle; prompts sign-in when signed out (see [saved homes](saved-homes.md)) |
| Title | Links to the property page |
| Area and type | "Dhamrai Bazar, Flat" |
| Facts | Bedrooms (or Studio), bathrooms, size if known, move-in |
| Cost | Brass double rule, "Total per month" with the total large; split line "Rent ৳X + service ৳Y + N more bill(s)"; "Advance: ৳Z" or "Not specified" |

## Behaviour

- Changing any filter updates the URL (replace, not push) and re-runs the search; text and number fields apply on change, not on every keystroke.
- Each chip has a remove button labelled "Remove {filter} filter". Rent min and max share one chip when both are set.
- "Clear all filters" resets every filter but keeps the sort.
- On phones, the sheet has "Clear all filters" and "Show results (n)" pinned at the bottom; Escape or the backdrop closes it.
- Results page by keyset cursor, 20 per page, with a "Show more homes" button. Ordering is stable across pages.
- The count is always the real number of matches.

## States

| State | Behaviour |
| --- | --- |
| No matches | "No homes match these filters" and "Changing one filter may show more homes", with buttons only for filters in use: Remove max rent, Search all of Dhamrai, Any number of bedrooms, plus Clear all filters. Never show placeholder listings. |
| Loading | Keep previous results dimmed with the count area announcing "Loading" to screen readers |
| Error | "Homes could not be loaded. Try again." with a retry button |
| Invalid URL values | Ignore unknown keys and invalid values; show the remaining filters |

## Data and API

`GET /v1/listings` with the URL keys above plus `cursor` and `size` (max 24). Response: `{ items: Card[], count, nextCursor }`. See [API](../tech/api.md#public).

## Analytics

`search_performed` on each search with filters, sort, result count and `source` (`home`, `search`).

## Acceptance criteria

- **SRCH-1** Given listings in all five areas, when I choose Dokkhin Para, then only Dokkhin Para listings show and the count matches.
- **SRCH-2** Given min 8,000 and max 12,000, then every result has monthly rent between 8,000 and 12,000 inclusive, even if its total is higher.
- **SRCH-3** Given "Who can rent: Family", then results include listings for families and listings for anyone, and exclude bachelor-only and student-only listings.
- **SRCH-4** Given I open a shared URL `/search?area=bazar&beds=2&sort=rent_asc`, then the same filters, chips and order appear.
- **SRCH-5** Given filters with no matches, then the empty state shows only the suggestion buttons for filters in use, and pressing one updates results.
- **SRCH-6** Given a listing that is expired, paused, rented, archived or hidden, then it never appears in results.
- **SRCH-7** Given a 375 px screen, when I tap "Filters", then a sheet opens, focus moves into it, and "Show results (n)" closes it.
- **SRCH-8** Given I remove the "2 bedrooms" chip, then the filter clears, the URL updates and the other filters stay.

## Out of scope

Map search, saved searches and alerts, radius search, price per square foot.

## Test cases

Dataset `pilot` ([test data](../tech/quality-and-testing.md#test-data-the-pilot-dataset)). E2E runs in `bn-mobile`, `bn-desktop` and `en-desktop` unless noted.

### E2E test cases

| ID | Covers | Preconditions | Steps | Expected |
| --- | --- | --- | --- | --- |
| SRCH-TC01 | SRCH-1 | Seed `pilot` | 1. Open `/{lang}/search`<br>2. Choose area Dokkhin Para | URL has `area=dokkhin`; count shows 3; cards are D1, D2, D3 |
| SRCH-TC02 | SRCH-2 | Seed `pilot` | 1. Open `/{lang}/search?min=8000&max=12000` | Count 7; every card's rent is 8,000 to 12,000; Z1 shows although its total is ৳14,080 |
| SRCH-TC03 | SRCH-3 | Seed `pilot` | 1. Open `/{lang}/search`<br>2. Choose "Who can rent: Family" | Count 11; no bachelor-only or student-only card (A3, B3, D2, Z2 absent) |
| SRCH-TC04 | SRCH-4 | Seed `pilot` | 1. Open `/{lang}/search?area=bazar&beds=2&sort=rent_asc` | Area select shows Dhamrai Bazar, bedrooms 2, sort "Rent: low to high"; chips for area and bedrooms; 1 result (Z1) |
| SRCH-TC05 | SRCH-5 | Seed `pilot` | 1. Open `/{lang}/search?area=barat&beds=3`<br>2. Press "Search all of Dhamrai" | Empty state offers "Search all of Dhamrai", "Any number of bedrooms" and "Clear all filters", but not "Remove max rent"; after step 2 the URL drops `area` and 4 results show (A1, D1, I1, I2) |
| SRCH-TC06 | SRCH-6 | Seed `pilot`; D1 paused through the test hook; M2 is past expiry; M5 is rented | 1. Open `/{lang}/search?area=dokkhin`<br>2. Open `/{lang}/search?area=islampur` | Dokkhin shows D2 and D3 only; Islampur shows I1 to I3, not M2; M5 never shows |
| SRCH-TC07 | SRCH-7 | Seed `pilot`; `bn-mobile` only | 1. Open `/bn/search`<br>2. Tap "Filters"<br>3. Choose 2 bedrooms<br>4. Tap "Show results (n)" | Sheet opens with focus inside; button shows the live count; sheet closes and results show 2-bedroom homes |
| SRCH-TC08 | SRCH-8 | Seed `pilot` | 1. Open `/{lang}/search?area=amin&beds=2`<br>2. Press the remove button on the bedrooms chip | URL keeps `area=amin`, drops `beds`; count goes from 1 to 3 |

### Automation (E2E)

```ts
// web/e2e/search.spec.ts
import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';
import { listing } from './seed-data';

test.beforeEach(async ({ seed }) => { await seed('pilot'); });
const cards = (page: Page) => page.getByTestId('results-list').getByRole('article');
// On phones the filters live in a sheet opened by the "Filters" button.
async function openFilters(page: Page, t: (k: string) => string) {
  if (test.info().project.name === 'bn-mobile') await page.getByRole('button', { name: new RegExp(t('filters')) }).click();
}

test('SRCH-TC01 area filter', { tag: '@smoke' }, async ({ page, lang, t, num }) => {
  await page.goto(`/${lang}/search`);
  await openFilters(page, t);
  await page.getByLabel(t('f_area'), { exact: true }).selectOption('dokkhin');
  await expect(page).toHaveURL(/area=dokkhin/);
  await expect(page.getByRole('status')).toHaveText(t('results_n', { n: num(3) }));
  for (const id of ['D1', 'D2', 'D3']) await expect(cards(page).filter({ hasText: listing(id).title[lang] })).toHaveCount(1);
});

test('SRCH-TC02 rent filter uses monthly rent, not the total', async ({ page, lang }) => {
  await page.goto(`/${lang}/search?min=8000&max=12000`);
  await expect(cards(page)).toHaveCount(7);
  await expect(cards(page).filter({ hasText: listing('Z1').title[lang] })).toHaveCount(1);
});

test('SRCH-TC03 family filter includes "anyone" listings', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/search`);
  await openFilters(page, t);
  await page.getByLabel(t('f_occ')).selectOption('family');
  await expect(cards(page)).toHaveCount(11);
  for (const id of ['A3', 'B3', 'D2', 'Z2']) await expect(page.getByText(listing(id).title[lang])).toHaveCount(0);
});

test('SRCH-TC04 shared URL restores filters, chips and sort', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/search?area=bazar&beds=2&sort=rent_asc`);
  await expect(page.getByLabel(t('f_area'), { exact: true })).toHaveValue('bazar');
  await expect(page.getByLabel(t('f_beds'), { exact: true })).toHaveValue('2');
  await expect(page.getByLabel(t('sort'))).toHaveValue('rent_asc');
  await expect(cards(page)).toHaveCount(1);
});

test('SRCH-TC05 empty state offers only relevant fixes', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/search?area=barat&beds=3`);
  await expect(page.getByRole('heading', { name: t('empty_t') })).toBeVisible();
  await expect(page.getByRole('button', { name: t('empty_area') })).toBeVisible();
  await expect(page.getByRole('button', { name: t('empty_beds') })).toBeVisible();
  await expect(page.getByRole('button', { name: t('empty_max') })).toHaveCount(0);
  await page.getByRole('button', { name: t('empty_area') }).click();
  await expect(page).not.toHaveURL(/area=/);
  await expect(cards(page)).toHaveCount(4);
});

test('SRCH-TC06 non-live listings never appear', async ({ page, lang, act }) => {
  await act('D1', 'pause');
  await page.goto(`/${lang}/search?area=dokkhin`);
  await expect(cards(page)).toHaveCount(2);
  await page.goto(`/${lang}/search?area=islampur`);
  await expect(page.getByText(listing('M2').title[lang])).toHaveCount(0);
  await expect(page.getByText(listing('M5').title[lang])).toHaveCount(0);
});

test('SRCH-TC07 mobile filter sheet', async ({ page, t }, info) => {
  test.skip(info.project.name !== 'bn-mobile');
  await page.goto('/bn/search');
  await page.getByRole('button', { name: new RegExp(t('filters')) }).click();
  await expect(page.getByLabel(t('f_area'), { exact: true })).toBeFocused();
  await page.getByLabel(t('f_beds'), { exact: true }).selectOption('2');
  await page.getByRole('button', { name: new RegExp(t('show_results')) }).click();
  await expect(page.getByLabel(t('f_beds'), { exact: true })).toBeHidden();
  await expect(page).toHaveURL(/beds=2/);
});

test('SRCH-TC08 removing one chip keeps the others', async ({ page, lang, t, num }) => {
  await page.goto(`/${lang}/search?area=amin&beds=2`);
  await expect(cards(page)).toHaveCount(1);
  await page.getByRole('button', { name: t('remove_filter', { x: t('beds_n', { n: num(2) }) }) }).click();
  await expect(page).toHaveURL(/area=amin/);
  await expect(page).not.toHaveURL(/beds=/);
  await expect(cards(page)).toHaveCount(3);
});
```

### API test cases

| ID | Covers | Case | Expected |
| --- | --- | --- | --- |
| SRCH-API01 | SRCH-2 | `GET /v1/listings?min=8000&max=12000` | `count` 7; every item's `rent` within 8,000 to 12,000 |
| SRCH-API02 | SRCH-6 | `GET /v1/listings?size=24` | `count` 15; M2, M5 and P1 absent |
| SRCH-API03 | Stable paging | Two pages of 10 using `nextCursor` | 15 distinct listings; second page has no `nextCursor` |

### Automation (API)

```csharp
// tests/BasaChai.Api.Tests/SearchTests.cs
public class SearchTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // SRCH-API01: SRCH-2
    public async Task Rent_filter_is_inclusive_and_uses_monthly_rent()
    {
        var client = await f.AsAsync("anonymous");
        var page = await client.GetFromJsonAsync<SearchPage>("/v1/listings?min=8000&max=12000");
        Assert.Equal(7, page!.Count);
        Assert.All(page.Items, c => Assert.InRange(c.Rent, 8_000, 12_000));
    }

    [Fact] // SRCH-API02: SRCH-6
    public async Task Expired_paused_rented_and_hidden_listings_are_excluded()
    {
        var client = await f.AsAsync("anonymous");
        var page = await client.GetFromJsonAsync<SearchPage>("/v1/listings?size=24");
        Assert.Equal(15, page!.Count);
        Assert.DoesNotContain(page.Items, c => c.ShortId is "M2" or "M5" or "P1");
    }

    [Fact] // SRCH-API03: keyset paging is stable
    public async Task Paging_returns_every_listing_once_in_order()
    {
        var client = await f.AsAsync("anonymous");
        var first = await client.GetFromJsonAsync<SearchPage>("/v1/listings?size=10");
        var second = await client.GetFromJsonAsync<SearchPage>($"/v1/listings?size=10&cursor={first!.NextCursor}");
        var ids = first.Items.Concat(second!.Items).Select(c => c.ShortId).ToList();
        Assert.Equal(15, ids.Distinct().Count());
        Assert.Null(second.NextCursor);
    }
}
```

## Related

[home](home.md) · [property page](property-page.md) · [listing and pricing](../domain/listing-and-pricing.md) · [API](../tech/api.md)
