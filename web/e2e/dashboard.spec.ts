// Extracted from specs/features/owner-dashboard.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
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
