# Home

The home page lets a tenant start a search in one step, shows what makes BasaChai different (the full monthly cost), and surfaces the 4 most recently confirmed homes.

Version 1.0 · 13 October 2026 · Route `/` (`/bn`, `/en`) · Covers FR-01, D20

## User stories

- As a tenant, I can search by area, maximum rent and bedrooms straight from the home page.
- As a tenant, I can see recently confirmed homes without searching.
- As a first-time visitor, I understand that BasaChai shows the real monthly cost and lets me call the owner directly.

## Layout

| Block | Content |
| --- | --- |
| Hero | Headline (brand promise), one-line description, search form |
| Search form | Area (All of Dhamrai + 5 pilot areas), Max rent (number, step 500), Bedrooms (Any, Studio, 1 to 4), "Search homes" button |
| Cost example | A sample cost breakdown from a real live listing, labelled "Example", with the caption explaining every listing shows costs this way |
| Recently confirmed homes | 4 listing cards, heading "Recently confirmed homes", link "See all homes" to `/search` |
| How it works | 3 numbered steps: search by area and budget; see the real monthly cost; call the owner directly |
| Safety tip | Icon, "See the home before you pay", one sentence on never sending money before visiting |

### Breakpoints

| Width | Hero | Recent homes grid |
| --- | --- | --- |
| Under 600 px | Stacked; form fields stacked | 1 per row |
| 600 to 899 px | Stacked; form fields in one row from 640 px | 2 x 2 |
| 900 to 1,099 px | Form left, cost example right | 2 x 2 |
| 1,100 px and up | Form left, cost example right | 4 in one row |

## Behaviour

- **Search form** builds the search URL with only the filled fields and navigates to `/search?...`. Empty fields are omitted.
- **Recent homes** are the 4 Live, unhidden, unexpired listings with the latest `last_confirmed_at`. If fewer than 4 exist, show what exists; the grid must not leave a single card alone on a row at 2 x 2 (with 3 listings, show 2).
- **Cost example** uses a Live listing chosen by an admin setting; if none is set, use the most recently confirmed listing that has at least one extra monthly cost.
- The page is server-rendered and cached at the edge for 60 seconds.

## States

| State | Behaviour |
| --- | --- |
| No live listings | Hide the recent homes section; keep the search form and how-it-works |
| API unavailable | Render the hero and static blocks; recent homes section shows "Homes could not be loaded. Try again." with a retry link |

## Data and API

- `GET /v1/listings?sort=confirmed&size=4` for recent homes.
- `GET /v1/locations` for the area options (cached).
- Card fields: see [search](search.md#card).

## Analytics

- `search_performed` when the home search form is submitted (with `source: home`).
- `listing_viewed` is recorded on the property page, not on the card.

## Copy

| Key | Bangla | English |
| --- | --- | --- |
| hero_title | বাসা খুঁজুন। মালিকের সাথে সরাসরি কথা বলুন। | Find a home. Talk to the owner directly. |
| hero_sub | ধামরাইয়ের ভাড়া বাসা। ভাড়া, সার্ভিস চার্জ আর অগ্রিম প্রতিটি আলাদা করে লেখা। | Rental homes in Dhamrai. Rent, service charge and advance are each listed separately. |
| hero_cap | প্রতিটি বিজ্ঞাপনে খরচ এভাবে ভেঙে দেখানো হয়, যাতে আসল মাসিক খরচ আগেই জানেন। | Every listing breaks down its costs like this, so you know the real monthly cost up front. |
| recent_t | সম্প্রতি নিশ্চিত করা বাসা | Recently confirmed homes |
| safety_t | টাকা দেওয়ার আগে বাসা নিজে দেখুন | See the home before you pay |

## Acceptance criteria

- **HOME-1** Given 15 live listings, when the home page loads, then exactly 4 cards show, ordered by most recent confirmation.
- **HOME-2** Given a 1,280 px screen, then the 4 cards sit in one row; at 768 px they form 2 x 2; at 375 px they stack with no horizontal scroll.
- **HOME-3** Given I choose Islampur, max rent 10,000 and 2 bedrooms, when I press "Search homes", then I land on `/search?area=islampur&max=10000&beds=2`.
- **HOME-4** Given an expired, paused or hidden listing was confirmed most recently, then it does not appear on the home page.
- **HOME-5** Given the English toggle is on, then all home copy shows in English and numbers use Latin digits.

## Out of scope

Personalised recommendations, featured listings, area guides.

## Test cases

Dataset `pilot` ([test data](../tech/quality-and-testing.md#test-data-the-pilot-dataset)). Runs in `bn-mobile`, `bn-desktop` and `en-desktop` unless noted.

### E2E test cases

| ID | Covers | Preconditions | Steps | Expected |
| --- | --- | --- | --- | --- |
| HOME-TC01 | HOME-1 | Seed `pilot` | 1. Open `/{lang}` | "Recently confirmed homes" shows 4 cards; the first is B1 |
| HOME-TC02 | HOME-2 | Seed `pilot` | 1. Set width 1,280, 768, then 375 px<br>2. Open `/{lang}` each time | Grid has 4, 2, then 1 column(s); page never scrolls sideways |
| HOME-TC03 | HOME-3 | Seed `pilot` | 1. Choose Islampur<br>2. Enter max rent 10000<br>3. Choose 2 bedrooms<br>4. Press "Search homes" | URL is `/{lang}/search?area=islampur&max=10000&beds=2`; 1 result (I3) |
| HOME-TC04 | HOME-4 | Seed `pilot`; B1 hidden through the test hook | 1. Open `/{lang}` | B1 is not shown; 4 other cards show |
| HOME-TC05 | HOME-5 | Seed `pilot`; `bn-desktop` only | 1. Open `/bn`<br>2. Press "English" | Headline is "Find a home. Talk to the owner directly."; the example total shows `৳12,780` in Latin digits |

### Automation

```ts
// web/e2e/home.spec.ts
import { test, expect } from './fixtures';
import { listing } from './seed-data';

test.beforeEach(async ({ seed }) => { await seed('pilot'); });

test('HOME-TC01 shows the 4 most recently confirmed homes', { tag: '@smoke' }, async ({ page, lang, t }) => {
  await page.goto(`/${lang}`);
  const recent = page.getByRole('region', { name: t('recent_t') });
  await expect(recent.getByRole('article')).toHaveCount(4);
  await expect(recent.getByRole('article').first()).toContainText(listing('B1').title[lang]);
});

for (const [width, columns] of [[1280, 4], [768, 2], [375, 1]] as const) {
  test(`HOME-TC02 recent homes use ${columns} column(s) at ${width}px`, async ({ page, lang }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.goto(`/${lang}`);
    const grid = page.getByTestId('recent-grid');
    const count = await grid.evaluate(el => getComputedStyle(el).gridTemplateColumns.split(' ').length);
    expect(count).toBe(columns);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  });
}

test('HOME-TC03 home search builds the search URL', async ({ page, lang, t }) => {
  await page.goto(`/${lang}`);
  await page.getByLabel(t('f_area')).selectOption('islampur');
  await page.getByLabel(t('f_max')).fill('10000');
  await page.getByLabel(t('f_beds')).selectOption('2');
  await page.getByRole('button', { name: t('search_btn') }).click();
  await expect(page).toHaveURL(new RegExp(`/${lang}/search\\?area=islampur&max=10000&beds=2$`));
  await expect(page.getByTestId('results-list').getByRole('article')).toHaveCount(1);
});

test('HOME-TC04 hidden listings never appear on the home page', async ({ page, lang, act }) => {
  await act('B1', 'hide');
  await page.goto(`/${lang}`);
  await expect(page.getByText(listing('B1').title[lang])).toHaveCount(0);
  await expect(page.getByTestId('recent-grid').getByRole('article')).toHaveCount(4);
});

test('HOME-TC05 language toggle switches copy and digits', async ({ page }, info) => {
  test.skip(info.project.name !== 'bn-desktop');
  await page.goto('/bn');
  await page.getByRole('button', { name: 'English' }).click();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Find a home. Talk to the owner directly.');
  await expect(page.getByTestId('cost-total').first()).toHaveText('৳12,780');
});
```

## Related

[search](search.md) · [design system](../design/design-system.md) · [listing and pricing](../domain/listing-and-pricing.md)
