# Owner dashboard (My listings)

Owners see every listing they have, what needs attention, and the actions allowed for each status. All reminders are computed when the page loads; nothing is sent by SMS, email or push (D11, D12).

Version 1.0 · 13 October 2026 · Route `/dashboard`, owners only · Covers FR-10, D7, D11, D23

## User stories

- As an owner, I see which listings are about to expire and confirm them in one tap.
- As an owner, I see moderator feedback and fix my listing.
- As an owner, I can pause, resume, mark rented, renew or archive a listing.
- As an owner, I can see how many people viewed my listing and my number.

## Layout

1. Heading "My listings" and a "New listing" button.
2. **Attention banners**, one per listing that needs action, in this order:

| Condition | Style | Text | Action |
| --- | --- | --- | --- |
| Changes needed | Red | A moderator asked for changes to "{title}". | Edit |
| Expired | Red | "{title}" has expired, so it is hidden from search. | Renew |
| Live and expires within 3 days | Brass | "{title}" expires in {n} days. Confirm it is still available. | Still available |

3. **Listing rows** sorted by: Changes needed, Draft, Expired, In review, Live, Paused, Rented, Rejected. Archived listings are hidden.

| Row element | Rule |
| --- | --- |
| Thumbnail | Cover photo |
| Status badge | See [design system](../design/design-system.md#status-badges) |
| Total | "৳X / Total per month" |
| Title | Links to the property page when Live |
| Meta | "{n} days left" (Live, Paused), "Usually reviewed within one business day" (In review), "{n} views", "{n} number views" |
| Moderator's note | For Changes needed and Rejected |
| Actions | By status, below |

### Actions by status

| Status | Actions |
| --- | --- |
| Draft | Continue (opens the posting steps where the owner left off), Archive |
| Live | Still available, Pause, Mark as rented, Edit |
| Paused | Show again, Mark as rented |
| Expired | Renew, Archive |
| Changes needed | Edit, Archive |
| In review | None |
| Rented, Rejected | Archive |

## Header badge

When signed in as an owner, "My listings" in the header shows a red count of listings that need attention (the banners above). On phones it sits on the bottom tab icon.

## Behaviour

- **Still available** (reconfirm) and **Renew** set a fresh 30 days; message "Confirmed. It stays in search for another 30 days." or "Renewed. It is in search for 30 days."
- **Pause** hides from search: "Paused. It is hidden from search." **Show again** returns it: "It is back in search." If its 30 days ran out while paused, it becomes Expired instead.
- **Mark as rented**: "Marked as rented and removed from search."
- **Archive** removes it from the dashboard: "Archived."
- Visiting the dashboard persists any read-time expiry for this owner's listings (see [lifecycle](../domain/listing-lifecycle.md#read-time-expiry)).

## States

| State | Behaviour |
| --- | --- |
| No listings | "No listings yet." with "New listing" |
| Not an owner | Sign-in dialog, then becomes owner |

## Data and API

- `GET /v1/owner/listings` returns rows with effective status, days left, views and reveals.
- `GET /v1/me/attention` returns the attention count for the header.
- `POST /v1/owner/listings/{id}/{reconfirm|pause|resume|mark-rented|renew|archive}`.

## Analytics

`listing_reconfirmed` on reconfirm and renew.

## Acceptance criteria

- **DASH-1** Given a Live listing confirmed 28 days ago, then a brass banner says it expires in 2 days, and "Still available" resets it to 30 days left.
- **DASH-2** Given a listing confirmed 31 days ago and never reconfirmed, then it shows as Expired with a red banner and Renew, and it is absent from search.
- **DASH-3** Given a moderator requested changes with a reason, then the row shows "Changes needed" and the moderator's note, and Edit opens the posting steps.
- **DASH-4** Given 3 listings need attention, then the header badge shows 3; after reconfirming one it shows 2.
- **DASH-5** Given I pause a Live listing, then it disappears from search immediately and shows "Paused" here.
- **DASH-6** Given I archive a listing, then it no longer appears on the dashboard but remains in the database.

## Out of scope

Push, SMS or email reminders; inquiry inbox; analytics charts.

## Test cases

Dataset `pilot`; signed in as `owner` (Nasrin), who has Z1 (Live, 2 days left), M1 (Draft, left at the Costs step), M2 (past expiry), M3 (Changes needed), M4 (In review) and M5 (Rented).

### E2E test cases

| ID | Covers | Preconditions | Steps | Expected |
| --- | --- | --- | --- | --- |
| DASH-TC01 | DASH-1 | Seed `pilot` | 1. Open `/{lang}/dashboard`<br>2. Press "Still available" in Z1's brass banner | Step 1: brass banner says Z1 expires in 2 days. Step 2: banner gone; Z1 row shows 30 days left; confirmation message shows |
| DASH-TC02 | DASH-2 | Seed `pilot` | 1. Open `/{lang}/dashboard`<br>2. Check `/{lang}/search?area=islampur`<br>3. Press "Renew" in M2's red banner<br>4. Check search again | M2 shows Expired with a red banner and is absent from search; after Renew it is Live and appears in Islampur results |
| DASH-TC03 | DASH-3 | Seed `pilot` | 1. Open `/{lang}/dashboard`<br>2. Press "Edit" on M3 | M3 row shows "Changes needed" and the moderator's note; Edit opens the posting steps with M3's data |
| DASH-TC04 | DASH-4 | Seed `pilot` | 1. Open `/{lang}/dashboard`<br>2. Reconfirm Z1 | Header badge on "My listings" shows 3, then 2 |
| DASH-TC05 | DASH-5 | Seed `pilot` | 1. Press "Pause" on Z1<br>2. Open `/{lang}/search?area=bazar` | Z1 row shows "Paused"; Z1 absent from search |
| DASH-TC06 | DASH-6 | Seed `pilot` | 1. Press "Archive" on M5 | M5 disappears from the dashboard; the API reports status `archived` |
| DASH-TC07 | Read-time expiry | Seed `pilot`; clock +3 days | 1. Open `/{lang}/dashboard` | Z1 now shows Expired with a red banner and is absent from search (no job ran) |

### Automation (E2E)

```ts
// web/e2e/dashboard.spec.ts
import type { Page } from '@playwright/test';
import { test, expect, type Lang } from './fixtures';
import { listing } from './seed-data';

test.beforeEach(async ({ seed, signInAs }) => { await seed('pilot'); await signInAs('owner'); });
const row = (page: Page, id: string, lang: Lang) => page.getByRole('article').filter({ hasText: listing(id).title[lang] });
const banners = (page: Page) => page.getByTestId('attention-banners');

test('DASH-TC01 reconfirm from the expiry reminder', { tag: '@smoke' }, async ({ page, lang, t, num }) => {
  await page.goto(`/${lang}/dashboard`);
  const banner = banners(page).getByText(t('al_expiring', { x: listing('Z1').title[lang], n: num(2) }));
  await expect(banner).toBeVisible();
  await banners(page).getByRole('button', { name: t('reconfirm') }).click();
  await expect(banner).toHaveCount(0);
  await expect(row(page, 'Z1', lang)).toContainText(t('days_left', { n: num(30) }));
  await expect(page.getByRole('status')).toHaveText(t('t_reconfirmed'));
});

test('DASH-TC02 renew an expired listing', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/search?area=islampur`);
  await expect(page.getByText(listing('M2').title[lang])).toHaveCount(0);
  await page.goto(`/${lang}/dashboard`);
  await expect(row(page, 'M2', lang)).toContainText(t('st_expired'));
  await banners(page).getByRole('button', { name: t('renew') }).click();
  await expect(row(page, 'M2', lang)).toContainText(t('st_published'));
  await page.goto(`/${lang}/search?area=islampur`);
  await expect(page.getByText(listing('M2').title[lang])).toHaveCount(1);
});

test('DASH-TC03 changes requested shows the note and opens editing', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/dashboard`);
  await expect(row(page, 'M3', lang)).toContainText(t('st_changes'));
  await expect(row(page, 'M3', lang)).toContainText(listing('M3').reason[lang]);
  await row(page, 'M3', lang).getByRole('button', { name: t('edit') }).click();
  await expect(page.getByLabel(t('l_title'))).toHaveValue(listing('M3').title[lang]);
});

test('DASH-TC04 header attention count', async ({ page, lang, t, num }) => {
  await page.goto(`/${lang}/dashboard`);
  const nav = page.getByRole('link', { name: new RegExp(t('nav_dash')) });
  await expect(nav).toContainText(num(3));
  await banners(page).getByRole('button', { name: t('reconfirm') }).click();
  await expect(nav).toContainText(num(2));
});

test('DASH-TC05 pause hides from search', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/dashboard`);
  await row(page, 'Z1', lang).getByRole('button', { name: t('pause') }).click();
  await expect(row(page, 'Z1', lang)).toContainText(t('st_paused'));
  await page.goto(`/${lang}/search?area=bazar`);
  await expect(page.getByText(listing('Z1').title[lang])).toHaveCount(0);
});

test('DASH-TC06 archive removes the row but keeps the record', async ({ page, lang, t, api }) => {
  await page.goto(`/${lang}/dashboard`);
  await row(page, 'M5', lang).getByRole('button', { name: t('archive') }).click();
  await expect(row(page, 'M5', lang)).toHaveCount(0);
  const m5 = await (await api.get('/v1/test/listings/M5')).json();
  expect(m5.status).toBe('archived');
});

test('DASH-TC07 expiry happens at read time', async ({ page, lang, t, clock }) => {
  await clock(3);
  await page.goto(`/${lang}/dashboard`);
  await expect(row(page, 'Z1', lang)).toContainText(t('st_expired'));
  await page.goto(`/${lang}/search?area=bazar`);
  await expect(page.getByText(listing('Z1').title[lang])).toHaveCount(0);
});
```

`GET /v1/test/listings/{id}` returns a listing's stored row for assertions (test environment only).

## Related

[listing lifecycle](../domain/listing-lifecycle.md) · [post a property](post-property.md) · [API](../tech/api.md#owner)
