// Extracted from specs/design/design-system.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/design.spec.ts
import AxeBuilder from '@axe-core/playwright';
import { test, expect } from './fixtures';

const ROUTES = ['', '/search', '/listing/D3', '/saved', '/dashboard', '/post', '/admin'];
test.beforeEach(async ({ seed }) => { await seed('pilot'); });

test('DS-TC01 light theme even in dark mode', async ({ page, lang }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  for (const path of ['', '/search', '/listing/D3']) {
    await page.goto(`/${lang}${path}`);
    expect(await page.evaluate(() => getComputedStyle(document.body).backgroundColor)).toBe('rgb(244, 246, 242)');
  }
});

test('DS-TC02 fonts per language', async ({ page, lang }) => {
  await page.goto(`/${lang}/listing/D3`);
  await page.evaluate(() => document.fonts.ready);
  const body = await page.evaluate(() => getComputedStyle(document.body).fontFamily);
  expect(body).toMatch(lang === 'bn' ? /^"?Noto Sans Bengali/ : /^"?Nunito/);
  expect(await page.getByRole('heading', { level: 1 }).evaluate(el => getComputedStyle(el).fontFamily)).toMatch(/^"?Baloo Da 2/);
  expect(await page.getByTestId('cost-total').evaluate(el => getComputedStyle(el).fontFamily)).toMatch(/^"?Baloo Da 2/);
});

test('DS-TC03 visible focus outline', async ({ page, lang }) => {
  await page.goto(`/${lang}`);
  for (let i = 0; i < 12; i++) {
    await page.keyboard.press('Tab');
    const outline = await page.evaluate(() => {
      const s = getComputedStyle(document.activeElement!);
      return `${s.outlineWidth} ${s.outlineStyle} ${s.outlineColor}`;
    });
    expect(outline).toBe('3px solid rgb(183, 134, 42)');
  }
});

for (const width of [360, 768, 1280]) {
  test(`DS-TC04 no sideways scroll and no axe violations at ${width}px`, async ({ page, lang, signInAs }) => {
    await signInAs('admin');                                  // admin can open every route
    await page.setViewportSize({ width, height: 900 });
    for (const path of ROUTES) {
      await page.goto(`/${lang}${path}`);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), path).toBe(true);
      const { violations } = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag22aa']).analyze();
      expect(violations, path).toEqual([]);
    }
  });
}
