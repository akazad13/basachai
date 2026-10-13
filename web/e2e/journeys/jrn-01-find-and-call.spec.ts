// Extracted from specs/journey.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/journeys/jrn-01-find-and-call.spec.ts
import { test, expect } from '../fixtures';

test('JRN-01 @journey a tenant finds a home and calls the owner', async ({ page, t, money, num, seed, api }) => {
  await seed('pilot');
  await page.goto('/bn');
  await page.getByLabel(t('f_area')).selectOption('dokkhin');
  await page.getByLabel(t('f_max')).fill('12000');
  await page.getByRole('button', { name: t('search_btn') }).click();
  await expect(page.getByRole('status')).toHaveText(t('results_n', { n: num(2) }));

  await page.getByLabel(t('sort')).selectOption('rent_asc');
  await expect(page).toHaveURL(/sort=rent_asc/);
  const cards = page.getByTestId('results-list').getByRole('article');
  await expect(cards.first()).toContainText(money(7_500 + 500));          // D2 total
  await cards.nth(1).getByRole('link').last().click();

  await expect(page.getByTestId('cost-total')).toHaveText(money(12_780));
  await expect(page.getByText(t('not_verified'))).toBeVisible();
  await page.getByRole('button', { name: t('show_number') }).click();
  await expect(page.getByRole('link', { name: t('call') })).toHaveAttribute('href', 'tel:+8801900001010');

  const events = await (await api.get('/v1/test/events?name=contact_revealed')).json();
  expect(events.at(-1).props.listingId).toBe('D3');
});
