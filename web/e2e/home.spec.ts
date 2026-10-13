// Extracted from specs/features/home.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
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
