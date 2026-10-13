# Saved homes

Signed-in users can save homes from any card or property page and see them together on one page.

Version 1.0 · 13 October 2026 · Route `/saved` · Covers FR-14 (Should)

## User stories

- As a tenant, I can save a home I like and come back to it later.
- As a tenant, I can unsave a home.

## Behaviour

- **Save button** appears on every card and on the property page: "☆ Save" when not saved, "★ Saved" when saved (`aria-pressed` reflects the state).
- **Signed out:** pressing Save opens the sign-in dialog with "Sign in to save homes."; after sign-in, the home is saved automatically.
- **Signed in:** pressing toggles immediately; messages "Added to saved homes" and "Removed from saved homes".
- **Saved page** shows cards (same as search) for saved homes that are still Live. Saved homes that are no longer Live are not shown.
- The "Saved" link appears in the header only when signed in.

## States

| State | Behaviour |
| --- | --- |
| Nothing saved | "You haven't saved any homes yet. Press Save on a home you like." with a "Find a home" button |
| Saved homes all unavailable | Same empty state |

## Data and API

- `GET /v1/me/saved` returns cards.
- `PUT /v1/me/saved/{listingId}` saves; `DELETE /v1/me/saved/{listingId}` unsaves. Both are idempotent.

## Acceptance criteria

- **SAVE-1** Given I am signed in, when I press Save on a card, then it shows "Saved" and the home appears on `/saved`.
- **SAVE-2** Given a saved home becomes rented, then it no longer appears on `/saved`.
- **SAVE-3** Given I am signed out, when I press Save and sign in, then the home is saved.
- **SAVE-4** Given nothing is saved, then the empty state and "Find a home" button show.

## Out of scope

Saved searches, alerts, sharing saved lists.

## Test cases

Dataset `pilot`. The seeded `tenant` has no saved homes.

### E2E test cases

| ID | Covers | Preconditions | Steps | Expected |
| --- | --- | --- | --- | --- |
| SAVE-TC01 | SAVE-1 | Seed `pilot`; signed in as `tenant` | 1. Open `/{lang}/search?area=amin`<br>2. Press "Save" on A1's card<br>3. Open `/{lang}/saved` | Step 2: button shows "Saved" with `aria-pressed="true"`. Step 3: one card, A1 |
| SAVE-TC02 | SAVE-2 | Signed in as `tenant`; Z3 saved | 1. Mark Z3 rented through the test hook<br>2. Open `/{lang}/saved` | Z3 is not shown; empty state shows |
| SAVE-TC03 | SAVE-3 | Signed out | 1. Open `/{lang}/listing/I1`<br>2. Press "Save" and sign in as `tenant` in the dialog<br>3. Open `/{lang}/saved` | I1 is listed without pressing Save again |
| SAVE-TC04 | SAVE-4 | Signed in as `tenant` | 1. Open `/{lang}/saved` | Empty-state text and a "Find a home" button that opens `/{lang}/search` |

### Automation (E2E)

```ts
// web/e2e/saved.spec.ts
import { test, expect } from './fixtures';
import { signInInDialog } from './helpers';
import { listing } from './seed-data';

test.beforeEach(async ({ seed }) => { await seed('pilot'); });

test('SAVE-TC01 save from a card', async ({ page, lang, t, signInAs }) => {
  await signInAs('tenant');
  await page.goto(`/${lang}/search?area=amin`);
  const card = page.getByRole('article').filter({ hasText: listing('A1').title[lang] });
  await card.getByRole('button', { name: t('save') }).click();
  await expect(card.getByRole('button', { name: t('saved') })).toHaveAttribute('aria-pressed', 'true');
  await page.goto(`/${lang}/saved`);
  await expect(page.getByRole('article')).toHaveCount(1);
  await expect(page.getByRole('article')).toContainText(listing('A1').title[lang]);
});

test('SAVE-TC02 saved homes that stop being live disappear', async ({ page, lang, t, signInAs, act }) => {
  await signInAs('tenant');
  await page.request.put(`${process.env.E2E_API_URL}/v1/me/saved/Z3`);
  await act('Z3', 'mark-rented');
  await page.goto(`/${lang}/saved`);
  await expect(page.getByRole('article')).toHaveCount(0);
  await expect(page.getByText(t('saved_empty'))).toBeVisible();
});

test('SAVE-TC03 save while signed out', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/listing/I1`);
  await page.getByRole('button', { name: t('save') }).click();
  await signInInDialog(page.getByRole('dialog'), 'tenant', t);
  await page.goto(`/${lang}/saved`);
  await expect(page.getByRole('article')).toContainText(listing('I1').title[lang]);
});

test('SAVE-TC04 empty state', async ({ page, lang, t, signInAs }) => {
  await signInAs('tenant');
  await page.goto(`/${lang}/saved`);
  await expect(page.getByText(t('saved_empty'))).toBeVisible();
  await page.getByRole('link', { name: t('nav_find') }).last().click();
  await expect(page).toHaveURL(new RegExp(`/${lang}/search`));
});
```

`signInInDialog(dialog, role, t)` lives in `web/e2e/helpers.ts`: it fills the seeded number for the role and `E2E_PASSWORD`, then presses "Sign in". The `PUT` in SAVE-TC02 is a plain API call from the signed-in page context; the test environment accepts it without the antiforgery header.

## Related

[auth and account](auth-and-account.md) · [search](search.md#card)
