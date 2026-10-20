// Extracted from specs/features/property-page.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/property.spec.ts
import { test, expect } from './fixtures';
import { listing } from './seed-data';

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

test('PROP-TC05 report needs a reason', async ({ page, lang, t }) => {
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
