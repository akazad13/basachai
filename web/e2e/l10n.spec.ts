// Extracted from specs/design/content-and-localization.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/l10n.spec.ts
import { test, expect } from './fixtures';

test.beforeEach(async ({ seed }) => { await seed('pilot'); });

test('L10N-TC01 Bangla digits everywhere in Bangla mode', async ({ page, t }, info) => {
  test.skip(!info.project.name.startsWith('bn'));
  for (const path of ['/bn/search', '/bn/listing/D3']) {
    await page.goto(path);
    if (path.includes('listing')) await page.getByRole('button', { name: t('show_number') }).click();
    expect(await page.locator('main').innerText(), path).not.toMatch(/[0-9]/);
  }
});

test('L10N-TC02 Latin digits and lakh grouping in English', async ({ page, act }, info) => {
  test.skip(info.project.name !== 'en-desktop');
  await act('A2', 'edit', { advance: { amount: 200000, notSpecified: false } });
  await page.goto('/en/listing/A2');
  await expect(page.getByTestId('cost-breakdown')).toContainText('৳2,00,000');
});

test('L10N-TC03 toggle keeps the listing; canonical and hreflang', async ({ page }, info) => {
  test.skip(info.project.name !== 'bn-desktop');
  await page.goto('/bn/listing/D3');
  await page.getByRole('button', { name: 'English' }).click();
  await expect(page).toHaveURL(/\/en\/.*D3/);
  await expect(page.locator('link[rel="canonical"]')).toHaveAttribute('href', /\/en\/rent\/dhaka\/dhamrai\/dokkhin\/.+-D3$/);
  await expect(page.locator('link[rel="alternate"][hreflang="bn"]')).toHaveCount(1);
  await expect(page.locator('link[rel="alternate"][hreflang="en"]')).toHaveCount(1);
});
