// Extracted from specs/domain/trust-and-safety.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/safety.spec.ts
import { test, expect } from './fixtures';
import { LIVE_IDS } from './seed-data';

const BANNED = [/\bverified\b/i, /\btrusted\b/i, /genuine owner/i, /যাচাইকৃত/, /বিশ্বস্ত/];

test('SAFE-TC01 no page claims verification', async ({ page, lang, t, seed }) => {
  await seed('pilot');
  for (const path of ['', '/search', ...LIVE_IDS.map(id => `/listing/${id}`)]) {
    await page.goto(`/${lang}${path}`);
    const text = (await page.locator('main').innerText()).replaceAll(t('not_verified'), '');
    for (const word of BANNED) expect(text, `${path} contains ${word}`).not.toMatch(word);
    if (path.startsWith('/listing/')) await expect(page.getByText(t('not_verified'))).toBeVisible();
  }
});
