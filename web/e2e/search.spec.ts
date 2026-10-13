// Extracted from specs/features/search.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
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
