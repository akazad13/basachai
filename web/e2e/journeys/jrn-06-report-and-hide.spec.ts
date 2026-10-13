// Extracted from specs/journey.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/journeys/jrn-06-report-and-hide.spec.ts
import { test, expect } from '../fixtures';
import { actor } from './actors';
import { listing } from '../seed-data';

test('JRN-06 @journey a tenant reports a scam and a moderator hides it', async ({ browser, page, t, seed, api }) => {
  await seed('pilot');
  const z2 = listing('Z2').title.bn;

  await page.goto('/bn/listing/Z2');
  await page.getByRole('button', { name: t('report') }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel(t('r_suspicious')).check();
  await dialog.getByLabel(t('r_details')).fill('দেখার আগেই বিকাশে টোকেন মানি চেয়েছে');
  await dialog.getByRole('button', { name: t('send_report') }).click();
  await expect(page.getByRole('status')).toHaveText(t('report_sent'));

  const mod = await actor(browser, { role: 'moderator' });
  await mod.goto('/bn/admin');
  const reports = mod.getByTestId('reports-list').getByRole('article').filter({ hasText: z2 });
  await expect(reports).toHaveCount(2);
  await reports.first().getByRole('button', { name: t('hide_listing') }).click();
  await expect(reports).toHaveCount(0);

  for (const path of ['/bn/search?area=bazar', '/bn']) {
    await page.goto(path);
    await expect(page.getByText(z2)).toHaveCount(0);
  }
  const audit = await (await api.get('/v1/test/audit?entity=listing&id=Z2')).json();
  expect(audit[0].action).toBe('listing.hide');
});
